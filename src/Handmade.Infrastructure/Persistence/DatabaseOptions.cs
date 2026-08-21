namespace Handmade.Infrastructure.Persistence;

/// <summary>
/// Strongly typed database connection options.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Prefer ConnectionStrings:Default in configuration; this property supports nested options if needed.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
