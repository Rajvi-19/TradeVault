using System.Globalization;
using Microsoft.Data.Sqlite;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed class TradeVaultService(PortableDatabase database) : ITradeVaultService
{
    private static string Text(string value, string field, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new BusinessRuleException($"{field} is required and must not exceed {max} characters.");
        return value.Trim();
    }
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? t, string sql,
        params (string Key, object? Value)[] parameters)
    {
        var command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = sql;
        foreach (var (key, value) in parameters) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }
    private static void Audit(SqliteConnection c, SqliteTransaction t, Guid entityId, string action)
    {
        using var command = Command(c, t, "INSERT INTO audit_events VALUES ($id,$entity,$action,$now)",
            ("$id", Guid.NewGuid().ToString()), ("$entity", entityId.ToString()),
            ("$action", action), ("$now", DateTimeOffset.UtcNow.ToString("O")));
        command.ExecuteNonQuery();
    }
    private static Party ReadParty(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)),
        r.GetString(1), r.GetString(2), r.GetInt32(3));
    private static Invoice ReadInvoice(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)),
        Guid.Parse(r.GetString(1)), r.GetString(2), ParseDate(r.GetString(3)),
        new(r.GetInt64(4)), new(r.GetInt64(5)), r.IsDBNull(6) ? null : ParseDate(r.GetString(6)),
        r.GetInt32(7), new(r.GetInt64(8)), r.GetInt32(9));
    private const string InvoiceColumns = "id,party_id,number,invoice_date,original_cents,bank_contract_cents,final_leg_bl_date,terms_days,shipping_cents,version";
    private static Party PartyById(SqliteConnection c, SqliteTransaction? t, Guid id)
    {
        using var command = Command(c, t, "SELECT id,name,country_code,default_terms_days FROM parties WHERE id=$id", ("$id", id.ToString()));
        using var r = command.ExecuteReader();
        if (!r.Read()) throw new BusinessRuleException("Party does not exist.");
        return ReadParty(r);
    }
    private static Invoice InvoiceById(SqliteConnection c, SqliteTransaction? t, Guid id)
    {
        using var command = Command(c, t, $"SELECT {InvoiceColumns} FROM invoices WHERE id=$id", ("$id", id.ToString()));
        using var r = command.ExecuteReader();
        if (!r.Read()) throw new BusinessRuleException("Invoice does not exist.");
        return ReadInvoice(r);
    }
    private static void ValidateInvoice(Usd original, Usd contract, Usd shipping, DateOnly? bl, int terms)
    {
        original.RequireNonNegative("Original amount");
        contract.RequireNonNegative("Bank contract amount");
        shipping.RequireNonNegative("Shipping charge");
        DueDates.Calculate(bl, terms);
    }
    private static void CheckNumber(SqliteConnection c, SqliteTransaction t, Guid party, string number, Guid except)
    {
        using var cmd = Command(c, t, "SELECT COUNT(*) FROM invoices WHERE party_id=$party AND number=$number COLLATE NOCASE AND id<>$id",
            ("$party", party.ToString()), ("$number", number), ("$id", except.ToString()));
        if (Convert.ToInt64(cmd.ExecuteScalar()) > 0)
            throw new BusinessRuleException("This invoice number already exists for the party. Include its financial year when needed.");
    }

    public Party CreateParty(CreateParty request)
    {
        var name = Text(request.Name, "Party name", 200);
        var country = Text(request.CountryCode, "Country code", 2).ToUpperInvariant();
        if (country.Length != 2 || country.Any(ch => ch is < 'A' or > 'Z'))
            throw new BusinessRuleException("Use a two-letter country code.");
        DueDates.Calculate(null, request.DefaultTermsDays);
        var party = new Party(Guid.NewGuid(), name, country, request.DefaultTermsDays);
        using var c = database.Open();
        using var t = c.BeginTransaction();
        using var cmd = Command(c, t, "INSERT INTO parties VALUES ($id,$name,$country,$terms)",
            ("$id", party.Id.ToString()), ("$name", name), ("$country", country), ("$terms", party.DefaultTermsDays));
        cmd.ExecuteNonQuery();
        Audit(c, t, party.Id, "PartyCreated");
        t.Commit();
        return party;
    }

    public IReadOnlyList<Party> GetParties()
    {
        using var c = database.Open();
        using var cmd = Command(c, null, "SELECT id,name,country_code,default_terms_days FROM parties ORDER BY name,id");
        using var r = cmd.ExecuteReader();
        var result = new List<Party>();
        while (r.Read()) result.Add(ReadParty(r));
        return result;
    }

    public Invoice CreateInvoice(CreateInvoice request)
    {
        var number = Text(request.Number, "Invoice number", 100);
        using var c = database.Open();
        using var t = c.BeginTransaction();
        var party = PartyById(c, t, request.PartyId);
        var terms = request.TermsDays ?? party.DefaultTermsDays;
        var shipping = request.ShippingCharge ?? Usd.FromDecimal(3800);
        ValidateInvoice(request.OriginalAmount, request.BankContractAmount, shipping, request.FinalLegBlDate, terms);
        var id = Guid.NewGuid();
        CheckNumber(c, t, party.Id, number, id);
        using var cmd = Command(c, t, """
            INSERT INTO invoices (id,party_id,number,invoice_date,original_cents,bank_contract_cents,final_leg_bl_date,terms_days,shipping_cents,version)
            VALUES ($id,$party,$number,$date,$original,$bank,$bl,$terms,$shipping,1)
            """, ("$id", id.ToString()), ("$party", party.Id.ToString()), ("$number", number),
            ("$date", Date(request.InvoiceDate)), ("$original", request.OriginalAmount.Cents),
            ("$bank", request.BankContractAmount.Cents), ("$bl", request.FinalLegBlDate is { } bl ? Date(bl) : null),
            ("$terms", terms), ("$shipping", shipping.Cents));
        cmd.ExecuteNonQuery();
        Audit(c, t, id, "InvoiceCreated");
        var invoice = InvoiceById(c, t, id);
        t.Commit();
        return invoice;
    }

    public Invoice UpdateInvoice(UpdateInvoice request)
    {
        var number = Text(request.Number, "Invoice number", 100);
        ValidateInvoice(request.OriginalAmount, request.BankContractAmount, request.ShippingCharge, request.FinalLegBlDate, request.TermsDays);
        using var c = database.Open();
        using var t = c.BeginTransaction();
        var previous = InvoiceById(c, t, request.Id);
        if (previous.Version != request.ExpectedVersion)
            throw new BusinessRuleException("Invoice changed since it was loaded. Refresh before saving.");
        using (var allocated = Command(c, t, "SELECT COALESCE(SUM(amount_cents),0) FROM receipt_allocations WHERE invoice_id=$id", ("$id", request.Id.ToString())))
        {
            if (request.BankContractAmount.Cents < Convert.ToInt64(allocated.ExecuteScalar()))
                throw new BusinessRuleException("Bank contract amount cannot be less than already allocated receipts.");
        }
        CheckNumber(c, t, previous.PartyId, number, request.Id);
        using (var dates = Command(c, t, """
            SELECT COUNT(*) FROM receipt_allocations a JOIN receipts r ON r.id=a.receipt_id
            WHERE a.invoice_id=$id AND r.receipt_date < $date
            """, ("$id", request.Id.ToString()), ("$date", Date(request.InvoiceDate))))
        {
            if (Convert.ToInt64(dates.ExecuteScalar()) > 0)
                throw new BusinessRuleException("Invoice date cannot be moved after its allocated receipts.");
        }
        using var cmd = Command(c, t, """
            UPDATE invoices SET number=$number,invoice_date=$date,original_cents=$original,
                bank_contract_cents=$bank,final_leg_bl_date=$bl,terms_days=$terms,shipping_cents=$shipping,version=version+1
            WHERE id=$id AND version=$version
            """, ("$number", number), ("$date", Date(request.InvoiceDate)), ("$original", request.OriginalAmount.Cents),
            ("$bank", request.BankContractAmount.Cents), ("$bl", request.FinalLegBlDate is { } bl ? Date(bl) : null),
            ("$terms", request.TermsDays), ("$shipping", request.ShippingCharge.Cents),
            ("$id", request.Id.ToString()), ("$version", request.ExpectedVersion));
        if (cmd.ExecuteNonQuery() != 1) throw new BusinessRuleException("Invoice changed. Refresh before saving.");
        Audit(c, t, request.Id, "InvoiceUpdated");
        var result = InvoiceById(c, t, request.Id);
        t.Commit();
        return result;
    }

    public IReadOnlyList<Invoice> GetInvoices(Guid partyId)
    {
        using var c = database.Open();
        PartyById(c, null, partyId);
        using var cmd = Command(c, null, $"SELECT {InvoiceColumns} FROM invoices WHERE party_id=$party ORDER BY invoice_date,number,id", ("$party", partyId.ToString()));
        using var r = cmd.ExecuteReader();
        var result = new List<Invoice>();
        while (r.Read()) result.Add(ReadInvoice(r));
        return result;
    }

    public Receipt RecordReceipt(CreateReceipt request)
    {
        ArgumentNullException.ThrowIfNull(request.Allocations);
        var allocations = request.Allocations.ToArray();
        request.Amount.RequirePositive("Receipt amount");
        if (request.Reference is null || request.Reference.Length > 200)
            throw new BusinessRuleException("Receipt reference must not exceed 200 characters.");
        if (allocations.Select(a => a.InvoiceId).Distinct().Count() != allocations.Length)
            throw new BusinessRuleException("Combine duplicate invoice allocations into one row.");
        foreach (var allocation in allocations) allocation.Amount.RequirePositive("Allocation");
        if (allocations.Sum(a => a.Amount.Cents) > request.Amount.Cents)
            throw new BusinessRuleException("Allocations exceed the receipt amount.");
        using var c = database.Open();
        // Immediate transaction: allocation checks and inserts share a single write lock.
        using var t = c.BeginTransaction(deferred: false);
        PartyById(c, t, request.PartyId);
        foreach (var allocation in allocations)
        {
            var invoice = InvoiceById(c, t, allocation.InvoiceId);
            if (invoice.PartyId != request.PartyId) throw new BusinessRuleException("Receipt and invoice must belong to the same party.");
            if (request.ReceiptDate < invoice.InvoiceDate)
                throw new BusinessRuleException("Allocate only to invoices dated on or before the receipt. Record earlier funds as unallocated advances.");
            using var total = Command(c, t, "SELECT COALESCE(SUM(amount_cents),0) FROM receipt_allocations WHERE invoice_id=$id", ("$id", invoice.Id.ToString()));
            if (allocation.Amount.Cents > invoice.BankContractAmount.Cents - Convert.ToInt64(total.ExecuteScalar()))
                throw new BusinessRuleException("Allocation exceeds the invoice's remaining bank contract balance.");
        }
        var id = Guid.NewGuid();
        using (var cmd = Command(c, t, "INSERT INTO receipts VALUES ($id,$party,$date,$amount,$reference)",
            ("$id", id.ToString()), ("$party", request.PartyId.ToString()), ("$date", Date(request.ReceiptDate)),
            ("$amount", request.Amount.Cents), ("$reference", request.Reference.Trim()))) cmd.ExecuteNonQuery();
        foreach (var allocation in allocations)
        {
            using var cmd = Command(c, t, "INSERT INTO receipt_allocations VALUES ($receipt,$invoice,$amount)",
                ("$receipt", id.ToString()), ("$invoice", allocation.InvoiceId.ToString()), ("$amount", allocation.Amount.Cents));
            cmd.ExecuteNonQuery();
        }
        Audit(c, t, id, "ReceiptRecorded");
        t.Commit();
        return new Receipt(id, request.PartyId, request.ReceiptDate, request.Amount, request.Reference.Trim(), Array.AsReadOnly(allocations));
    }

    public IReadOnlyList<Receipt> GetReceipts(Guid partyId)
    {
        using var c = database.Open();
        using var t = c.BeginTransaction(deferred: true);
        PartyById(c, t, partyId);
        var result = new List<Receipt>();
        using (var cmd = Command(c, t, "SELECT id,receipt_date,amount_cents,reference FROM receipts WHERE party_id=$party ORDER BY receipt_date,id", ("$party", partyId.ToString())))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) result.Add(new Receipt(Guid.Parse(r.GetString(0)), partyId, ParseDate(r.GetString(1)), new(r.GetInt64(2)), r.GetString(3), []));
        for (var index = 0; index < result.Count; index++)
        {
            using var cmd = Command(c, t, "SELECT invoice_id,amount_cents FROM receipt_allocations WHERE receipt_id=$id ORDER BY invoice_id", ("$id", result[index].Id.ToString()));
            using var r = cmd.ExecuteReader();
            var allocations = new List<ReceiptAllocation>();
            while (r.Read()) allocations.Add(new(Guid.Parse(r.GetString(0)), new(r.GetInt64(1))));
            result[index] = result[index] with { Allocations = allocations.AsReadOnly() };
        }
        t.Commit();
        return result;
    }

    public IReadOnlyList<InvoiceBalance> GetBankContractBalances(Guid partyId, DateOnly asOf)
    {
        using var c = database.Open();
        using var t = c.BeginTransaction(deferred: true);
        PartyById(c, t, partyId);
        using var cmd = Command(c, t, $"""
            SELECT {string.Join(",", InvoiceColumns.Split(',').Select(column => "i." + column))},
                COALESCE((SELECT SUM(a.amount_cents) FROM receipt_allocations a
                  JOIN receipts r ON r.id=a.receipt_id WHERE a.invoice_id=i.id AND r.receipt_date <= $asOf),0)
            FROM invoices i WHERE i.party_id=$party AND i.invoice_date <= $asOf
            ORDER BY i.invoice_date,i.number,i.id
            """, ("$party", partyId.ToString()), ("$asOf", Date(asOf)));
        var result = new List<InvoiceBalance>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var invoice = ReadInvoice(r);
                var allocated = r.GetInt64(10);
                var outstanding = invoice.BankContractAmount.Cents - allocated;
                var overdue = outstanding > 0 && invoice.DueDate is { } due ? Math.Max(0, asOf.DayNumber - due.DayNumber) : 0;
                result.Add(new(invoice, new(allocated), new(outstanding), overdue));
            }
        }
        t.Commit();
        return result;
    }
}
