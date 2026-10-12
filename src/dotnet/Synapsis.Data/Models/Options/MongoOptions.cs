namespace Synapsis.Data.Models.Options;

/// <summary>
/// Where MongoDB is, from the configuration section "Mongo" (sharedsettings.json; environment variables Mongo__Url,
/// Mongo__Database override it).
/// </summary>
public sealed class MongoOptions
{
    /// <summary>The connection string.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The database name.</summary>
    public string Database { get; set; } = string.Empty;
}
