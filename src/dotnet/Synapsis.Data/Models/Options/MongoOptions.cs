namespace Synapsis.Data.Models.Options;

/// <summary>Where MongoDB is, from the configuration section "Mongo" (environment variables Mongo__Url, Mongo__Database).</summary>
public sealed class MongoOptions
{
    /// <summary>The connection string.</summary>
    public string Url { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    /// <summary>The database name.</summary>
    public string Database { get; set; } = "synapsis_v2";
}
