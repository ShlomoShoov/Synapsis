using Synapsis.Sil.Models.Results;

namespace Synapsis.Sil.Models.Descriptions;

/// <summary>
/// The stable error codes a <see cref="Problem"/> can carry. They are a contract: every Synapsis API returns exactly
/// these strings for the same input (tests/conformance). The record codes are listed in the order the rules check them.
/// </summary>
public static class ErrorCodes
{
    /// <summary>A request body that is not a bundle: not JSON, or the bundle envelope is wrong.</summary>
    public const string Bundle = "E-BUNDLE";

    /// <summary>A schema package that breaks the package format (schemas/sil/schema-package.schema.json).</summary>
    public const string SchemaPackage = "E-SCHEMA-PACKAGE";

    /// <summary>A vocabulary code defined by two packages: every code has one owner.</summary>
    public const string SchemaOwnership = "E-SCHEMA-OWNERSHIP";

    /// <summary>Rule 1: "kind" is not entity, event, relation or conclusion (a source sent as a record, a typo).</summary>
    public const string Kind = "E-KIND";

    /// <summary>
    /// Rule 2 and a few later checks: the record's structure is wrong (a missing or unknown field, a wrong value type,
    /// an impossible date, a point out of range), or a closed-list value is unknown (reliability basis, method.by).
    /// </summary>
    public const string Schema = "E-SCHEMA";

    /// <summary>Rule 3: the source is not declared in any schema package.</summary>
    public const string RegistrySource = "E-REGISTRY-SOURCE";

    /// <summary>Rule 4: the id's prefix does not match the kind, or its namespace is not owned by the source's package.</summary>
    public const string IdNamespace = "E-ID-NAMESPACE";

    /// <summary>Rule 5: an entity type that is not registered.</summary>
    public const string RegistryEntityType = "E-REGISTRY-ENTITY-TYPE";

    /// <summary>Rule 5: an event type that is not registered.</summary>
    public const string RegistryEventType = "E-REGISTRY-EVENT-TYPE";

    /// <summary>Rule 5: a relation type that is not registered.</summary>
    public const string RegistryRelationType = "E-REGISTRY-RELATION-TYPE";

    /// <summary>Rule 5: a conclusion type that is not registered.</summary>
    public const string RegistryConclusionType = "E-REGISTRY-CONCLUSION-TYPE";

    /// <summary>Rule 6: the start is not aligned to its precision (a "month" starting on the 15th), or the precision is unknown.</summary>
    public const string TimeAlign = "E-TIME-ALIGN";

    /// <summary>Rule 6: the end is not after the start.</summary>
    public const string TimeOrder = "E-TIME-ORDER";

    /// <summary>Rule 7: known_at is before the time the record started.</summary>
    public const string KnownBeforeOccurrence = "E-KNOWN-BEFORE-OCCURRENCE";

    /// <summary>Rule 7: known_at is more than 5 minutes after the moment the server received the record.</summary>
    public const string KnownInFuture = "E-KNOWN-IN-FUTURE";

    /// <summary>Rule 8: the place has none of the four forms (point, admin, logical, unplaced).</summary>
    public const string PlaceMissing = "E-PLACE-MISSING";

    /// <summary>Rule 8: "unplaced" is combined with another place form.</summary>
    public const string PlaceConflict = "E-PLACE-CONFLICT";

    /// <summary>Rule 8: a place form the event's type does not allow.</summary>
    public const string PlaceForm = "E-PLACE-FORM";

    /// <summary>Rule 8: an unknown country code, or a subdivision of another country.</summary>
    public const string PlaceAdmin = "E-PLACE-ADMIN";

    /// <summary>Rules 8 and 12: a logical space that is not registered.</summary>
    public const string LogicalSpace = "E-LOGICAL-SPACE";

    /// <summary>Rules 8 and 12: a logical path deeper than its space allows (or empty, for a classification).</summary>
    public const string LogicalDepth = "E-LOGICAL-DEPTH";

    /// <summary>Rules 8 and 12: a value outside a closed logical space (rule A-CLOSED).</summary>
    public const string LogicalValue = "E-LOGICAL-VALUE";

    /// <summary>Rule 9: a measure the event's type does not allow, or the same measure twice.</summary>
    public const string Measure = "E-MEASURE";

    /// <summary>Rule 9: a measure in another unit than the registered one.</summary>
    public const string Unit = "E-UNIT";

    /// <summary>Rule 9: a negative value for a measure that is not signed (counts are never negative).</summary>
    public const string MeasureNegative = "E-MEASURE-NEGATIVE";

    /// <summary>Rule 9: a value below the measure's minimum or above its maximum.</summary>
    public const string MeasureRange = "E-MEASURE-RANGE";

    /// <summary>Rule 10: text on a type that carries none, or no title and no body on a type that requires text.</summary>
    public const string Text = "E-TEXT";

    /// <summary>Rule 11: an undeclared attribute, a value that breaks its declaration, or a required attribute missing.</summary>
    public const string Attribute = "E-ATTRIBUTE";

    /// <summary>Rule 13: the same id from the same source twice in one batch, with different content.</summary>
    public const string IdConflict = "E-ID-CONFLICT";

    /// <summary>Rule 13: a role the event's type does not allow, or an entity of a type the role does not take.</summary>
    public const string Role = "E-ROLE";

    /// <summary>Rule 13: a referenced record that is neither stored nor accepted earlier in the batch.</summary>
    public const string RefMissing = "E-REF-MISSING";

    /// <summary>Rule 13: a relation end whose entity type the relation type does not allow.</summary>
    public const string RelationDomain = "E-RELATION-DOMAIN";

    /// <summary>Rule 13: a conclusion premise that is neither stored nor accepted earlier in the batch.</summary>
    public const string PremiseMissing = "E-PREMISE-MISSING";
}
