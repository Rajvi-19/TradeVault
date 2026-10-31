using TradeVault.Domain;

namespace TradeVault.Application;

public sealed record CreateParty(string Name, string CountryCode, int DefaultTermsDays = 90);
public sealed record CreateInvoice(Guid PartyId, string Number, DateOnly InvoiceDate,
    Usd OriginalAmount, Usd BankContractAmount, DateOnly? FinalLegBlDate = null,
    int? TermsDays = null);
public sealed record UpdateInvoice(Guid Id, int ExpectedVersion, string Number,
    DateOnly InvoiceDate, Usd OriginalAmount, Usd BankContractAmount,
    DateOnly? FinalLegBlDate, int TermsDays);
public sealed record CreateReceipt(Guid PartyId, DateOnly ReceiptDate, Usd Amount,
    string Reference, IReadOnlyList<ReceiptAllocation> Allocations);

/// <summary>
/// Synchronous local SQLite boundary. Desktop should execute IO off the UI thread.
/// BusinessRuleException is safe to display; IO/database failures need a friendly UI message.
/// No login/role enforcement is implemented in this first storage milestone.
/// </summary>
public interface ITradeVaultService
{
    Party CreateParty(CreateParty request);
    IReadOnlyList<Party> GetParties();
    Invoice CreateInvoice(CreateInvoice request);
    Invoice UpdateInvoice(UpdateInvoice request);
    IReadOnlyList<Invoice> GetInvoices(Guid partyId);
    Receipt RecordReceipt(CreateReceipt request);
    IReadOnlyList<Receipt> GetReceipts(Guid partyId);
    IReadOnlyList<InvoiceBalance> GetBankContractBalances(Guid partyId, DateOnly asOf);
}
