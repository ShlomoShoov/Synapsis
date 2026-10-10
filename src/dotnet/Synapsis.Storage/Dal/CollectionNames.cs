namespace Synapsis.Storage.Dal;

/// <summary>The collections of the database, named in one place.</summary>
public static class CollectionNames
{
    /// <summary>Every version of every statement (record id + source), one document each.</summary>
    public const string Records = "records";

    /// <summary>The current text of each schema package, one document each.</summary>
    public const string SchemaPackages = "schema_packages";
}
