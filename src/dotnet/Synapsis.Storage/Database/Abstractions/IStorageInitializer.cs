using System.Threading.Tasks;

namespace Synapsis.Storage.Database.Abstractions;

/// <summary>Prepares the database once at start-up: the indexes every query needs. Safe to run again.</summary>
public interface IStorageInitializer
{
    /// <summary>Creates whatever is missing.</summary>
    Task InitializeAsync();
}
