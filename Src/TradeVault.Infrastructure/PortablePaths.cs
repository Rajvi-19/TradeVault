namespace TradeVault.Infrastructure;

/// <summary>Application root must be the USB folder in production. Explicit roots enable tests.</summary>
public sealed class PortablePaths
{
    public string Root { get; }
    public string Data { get; }
    public string Database { get; }
    public PortablePaths(string applicationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationRoot);
        if (!Path.IsPathFullyQualified(applicationRoot))
            throw new ArgumentException("An absolute application directory is required.", nameof(applicationRoot));
        Root = Path.GetFullPath(applicationRoot);
        Data = Path.Combine(Root, "Data");
        Database = Path.Combine(Data, "tradevault.db");
    }
    public static PortablePaths FromApplicationDirectory() => new(AppContext.BaseDirectory);
    internal void EnsureDirectories()
    {
        Directory.CreateDirectory(Data);
    }
}
