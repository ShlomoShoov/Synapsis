namespace Synapsis.Storage.Database.Models.Configuration;

/// <summary>
/// Where the database is, from the configuration section "Mongo" (environment variables Mongo__Url and
/// Mongo__Database).
/// </summary>
public sealed class MongoOptions
{
    /// <summary>The connection string, with the replica set that transactions and change streams need.</summary>
    public string Url { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    /// <summary>The database name ("synapsis_next" for the C# API while it runs beside the stand-in).</summary>
    public string Database { get; set; } = "synapsis_next";
}
