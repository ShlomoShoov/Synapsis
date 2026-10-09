using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Synapsis.Api.Registration.Services.Orchestrators;
using Synapsis.Api.Startup.Services.Initializers;
using Synapsis.Api.Tests.Support;
using Synapsis.Api.Vocabulary.Services.Providers;
using Synapsis.Sil;
using Synapsis.Storage.Database.Abstractions;
using Synapsis.Storage.Packages.Models.Domain;
using Xunit;

namespace Synapsis.Api.Tests.Startup;

/// <summary>Start-up: the database is prepared, core is seeded when missing, the vocabulary is loaded.</summary>
public class StartupInitializerTests
{
    private readonly FakePackageRepository _packages = new FakePackageRepository();
    private readonly VocabularyProvider _vocabulary = new VocabularyProvider();
    private readonly CountingInitializer _storage = new CountingInitializer();

    /// <summary>An empty store gets core, and the vocabulary is in force.</summary>
    [Fact]
    public async Task An_empty_store_gets_core_and_a_vocabulary()
    {
        await Initializer().StartAsync(CancellationToken.None);
        Assert.Equal(1, _storage.Calls);
        Assert.Equal(1, _packages.Stored["core"].Version);
        Assert.True(_vocabulary.Current.IsPrecision("day"));
    }

    /// <summary>A store that has core is not seeded again; every stored package is in the vocabulary.</summary>
    [Fact]
    public async Task A_store_with_core_loads_what_is_stored()
    {
        _packages.Stored["core"] = Stored("core", 3);
        _packages.Stored["usgs"] = Stored("usgs", 1);
        await Initializer().StartAsync(CancellationToken.None);
        Assert.Equal(3, _packages.Stored["core"].Version);
        Assert.NotNull(_vocabulary.Current.FindEventType("GEO.EARTHQUAKE"));
    }

    /// <summary>The initializer over the fakes and the real record-language components.</summary>
    private StartupInitializer Initializer()
    {
        RegistrationOrchestrator registration = new RegistrationOrchestrator(SilComponents.CreatePackageReader(), _packages, SilComponents.CreateFingerprinter(), SilComponents.CreatePackageValidator(), _vocabulary);
        return new StartupInitializer(_storage, _packages, registration, SilComponents.CreateRegistryBuilder(), _vocabulary, NullLogger<StartupInitializer>.Instance);
    }

    /// <summary>A real package as stored at this version.</summary>
    private static StoredPackage Stored(string name, int version)
    {
        string text = TestPackages.Text(name);
        return new StoredPackage
        {
            Name = name,
            Version = version,
            Fingerprint = "fp",
            Text = text,
            Package = SilComponents.CreatePackageReader().Read(text).Value!,
        };
    }

    /// <summary>A storage initializer that only counts its calls.</summary>
    private sealed class CountingInitializer : IStorageInitializer
    {
        /// <summary>How many times it ran.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public Task InitializeAsync()
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
