using System.Threading.Tasks;
using Synapsis.Api.Registration.Abstractions;
using Synapsis.Api.Registration.Models.Requests;
using Synapsis.Api.Registration.Models.Results;
using Synapsis.Api.Registration.Services.Orchestrators;
using Synapsis.Api.Tests.Support;
using Synapsis.Api.Vocabulary.Services.Providers;
using Synapsis.Sil;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Registry.Abstractions;
using Xunit;

namespace Synapsis.Api.Tests.Registration;

/// <summary>The registration process with a package store in memory and the real record-language components.</summary>
public class RegistrationOrchestratorTests
{
    private readonly FakePackageRepository _packages = new FakePackageRepository();
    private readonly VocabularyProvider _vocabulary = new VocabularyProvider();
    private readonly IRegistrationOrchestrator _registration;

    /// <summary>A registration over an empty store.</summary>
    public RegistrationOrchestratorTests()
    {
        _registration = new RegistrationOrchestrator(SilComponents.CreatePackageReader(), _packages, SilComponents.CreateFingerprinter(), SilComponents.CreatePackageValidator(), _vocabulary);
    }

    /// <summary>Core, then a feeder's package: each is created as version 1 and its codes are in force.</summary>
    [Fact]
    public async Task New_packages_are_created()
    {
        RegistrationResult core = await RegisterAsync("core", TestPackages.Text("core"));
        RegistrationResult usgs = await RegisterAsync("usgs", TestPackages.Text("usgs"));
        Assert.Equal(RegistrationStatus.Created, core.Status);
        Assert.Equal(RegistrationStatus.Created, usgs.Status);
        Assert.Equal(1, usgs.Version);
        Assert.NotNull(_vocabulary.Current.FindEventType("GEO.EARTHQUAKE"));
    }

    /// <summary>The same package again, even with another comment, is unchanged and keeps its version.</summary>
    [Fact]
    public async Task The_same_package_again_is_unchanged()
    {
        await RegisterAsync("core", TestPackages.Text("core"));
        await RegisterAsync("usgs", TestPackages.Text("usgs"));
        RegistrationResult again = await RegisterAsync("usgs", "# sent again\n" + TestPackages.Text("usgs"));
        Assert.Equal(RegistrationStatus.Unchanged, again.Status);
        Assert.Equal(1, again.Version);
    }

    /// <summary>A compatible change is the next version, and the new vocabulary is put in force.</summary>
    [Fact]
    public async Task A_compatible_change_is_an_update()
    {
        await RegisterAsync("core", TestPackages.Text("core"));
        await RegisterAsync("usgs", TestPackages.Text("usgs"));
        IRegistry before = _vocabulary.Current;
        RegistrationResult updated = await RegisterAsync("usgs", TestPackages.Text("usgs").Replace("min: -10, max: 800}", "min: -10, max: 700}"));
        Assert.Equal(RegistrationStatus.Updated, updated.Status);
        Assert.Equal(2, updated.Version);
        Assert.NotSame(before, _vocabulary.Current);
        Assert.Equal(700, _vocabulary.Current.FindMeasure("depth_km")!.Max);
    }

    /// <summary>A breaking change is refused, and the store and the vocabulary stay as they were.</summary>
    [Fact]
    public async Task A_breaking_change_is_refused_and_nothing_changes()
    {
        await RegisterAsync("core", TestPackages.Text("core"));
        await RegisterAsync("usgs", TestPackages.Text("usgs"));
        IRegistry before = _vocabulary.Current;
        string breaking = TestPackages.Text("usgs").Replace("measures: [magnitude, depth_km]", "measures: [magnitude]");
        Result<RegistrationResult> result = await _registration.RegisterAsync(new RegistrationRequest("usgs", breaking));
        Assert.Equal(ErrorCodes.SchemaBreaking, result.Problem!.Code);
        Assert.Same(before, _vocabulary.Current);
        Assert.Equal(1, _packages.Stored["usgs"].Version);
    }

    /// <summary>A package sent under another package's name is refused.</summary>
    [Fact]
    public async Task A_package_under_another_name_is_refused()
    {
        await RegisterAsync("core", TestPackages.Text("core"));
        Result<RegistrationResult> result = await _registration.RegisterAsync(new RegistrationRequest("kev", TestPackages.Text("usgs")));
        Assert.Equal(ErrorCodes.SchemaPackage, result.Problem!.Code);
    }

    /// <summary>Registers a package that must be accepted.</summary>
    private async Task<RegistrationResult> RegisterAsync(string name, string text)
    {
        Result<RegistrationResult> result = await _registration.RegisterAsync(new RegistrationRequest(name, text));
        Assert.True(result.Succeeded, $"{name}: {result.Problem?.Code} {result.Problem?.Message}");
        return result.Value!;
    }
}
