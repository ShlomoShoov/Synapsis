using System;
using MongoDB.Driver;
using Synapsis.Storage.Database.Models.Configuration;

namespace Synapsis.IntegrationTests.Support;

/// <summary>
/// A fresh database for one test class, deleted when the class is done. MongoDB is the core deployment's
/// (docker compose -f deploy/core/compose.yml up -d mongo); SYNAPSIS_TEST_MONGO can point elsewhere.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    /// <summary>The MongoDB address the tests use.</summary>
    public static readonly string Url = Environment.GetEnvironmentVariable("SYNAPSIS_TEST_MONGO")
        ?? "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    /// <summary>Creates a database name of its own: synapsis_test_ plus a random suffix.</summary>
    public TestDatabase()
    {
        Options = new MongoOptions { Url = Url, Database = "synapsis_test_" + Guid.NewGuid().ToString("N").Substring(0, 12) };
    }

    /// <summary>Where the test's database is.</summary>
    public MongoOptions Options { get; }

    /// <summary>Deletes the database.</summary>
    public void Dispose()
    {
        new MongoClient(Options.Url).DropDatabase(Options.Database);
    }
}
