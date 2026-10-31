namespace TradeVault.Domain;

public sealed class BusinessRuleException(string message) : Exception(message);

/// <summary>USD only. Reject sub-cent inputs instead of silently rounding them.</summary>
public readonly record struct Usd(long Cents)
{
    public decimal Amount => Cents / 100m;
    public static Usd FromDecimal(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new BusinessRuleException("USD amounts must have at most two decimal places.");
        return new Usd(checked((long)(amount * 100m)));
    }
    public void RequirePositive(string field)
    {
        if (Cents <= 0) throw new BusinessRuleException($"{field} must be greater than zero.");
    }
    public void RequireNonNegative(string field)
    {
        if (Cents < 0) throw new BusinessRuleException($"{field} cannot be negative.");
    }
}

public sealed record Party(Guid Id, string Name, string CountryCode, int DefaultTermsDays);

// This milestone supports one final-leg BL date per invoice; multi-BL scheduling is deferred.
public sealed record Invoice(Guid Id, Guid PartyId, string Number, DateOnly InvoiceDate,
    Usd OriginalAmount, Usd BankContractAmount, DateOnly? FinalLegBlDate, int TermsDays,
    int Version)
{
    public DateOnly? DueDate => DueDates.Calculate(FinalLegBlDate, TermsDays);
}

public static class DueDates
{
    public static DateOnly? Calculate(DateOnly? blDate, int termsDays)
    {
        if (termsDays is < 0 or > 3650)
            throw new BusinessRuleException("Payment terms must be between 0 and 3650 calendar days.");
        if (blDate is null) return null;
        if (blDate.Value.DayNumber > DateOnly.MaxValue.DayNumber - termsDays)
            throw new BusinessRuleException("The BL date and payment terms exceed the supported date range.");
        return blDate.Value.AddDays(termsDays);
    }
}

public sealed record ReceiptAllocation(Guid InvoiceId, Usd Amount);
public sealed record Receipt(Guid Id, Guid PartyId, DateOnly ReceiptDate, Usd Amount,
    string Reference, IReadOnlyList<ReceiptAllocation> Allocations)
{
    public Usd Unallocated => new(checked(Amount.Cents - Allocations.Sum(a => a.Amount.Cents)));
}

// Explicitly bank-contract receivables only, not the unresolved difference calculation.
public sealed record InvoiceBalance(Invoice Invoice, Usd AllocatedReceipts, Usd Outstanding,
    int OverdueDays);
