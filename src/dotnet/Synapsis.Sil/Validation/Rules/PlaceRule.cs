using System.Collections.Generic;
using Synapsis.Sil.Records;
using Synapsis.Sil.Records.Parts;
using Synapsis.Sil.Registry;
using Synapsis.Sil.Vocabulary.Terms;
using Synapsis.Sil.Vocabulary.Types;

namespace Synapsis.Sil.Validation.Rules;

/// <summary>
/// Rule 8 (events): the place must have a form, only forms its event type allows, a point in range with its
/// uncertainty, real ISO codes, and a registered logical position within its space's depth and closed values.
/// </summary>
public sealed class PlaceRule : IRecordRule
{
    /// <summary>Returns E-PLACE-*, E-LOGICAL-*, or E-SCHEMA for the point, or null. Other kinds pass.</summary>
    public RecordError? Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord)
        {
            return null;
        }
        Place place = eventRecord.Place;
        EventTypeDefinition type = context.Registry.EventTypes[eventRecord.Type];
        RecordError? error = CheckForms(place, type);
        if (error == null)
        {
            error = CheckPoint(place);
        }
        if (error == null && place.Admin != null)
        {
            error = CheckAdmin(place.Admin, context.Registry);
        }
        if (error == null && place.Logical != null)
        {
            error = CheckLogical(place.Logical, context.Registry);
        }
        return error;
    }

    /// <summary>The forms present, in the fixed order point, admin, logical, unplaced.</summary>
    private static List<PlaceForm> FormsOf(Place place)
    {
        List<PlaceForm> forms = new List<PlaceForm>();
        if (place.Point != null)
        {
            forms.Add(PlaceForm.Point);
        }
        if (place.Admin != null)
        {
            forms.Add(PlaceForm.Admin);
        }
        if (place.Logical != null)
        {
            forms.Add(PlaceForm.Logical);
        }
        if (place.Unplaced != null)
        {
            forms.Add(PlaceForm.Unplaced);
        }
        return forms;
    }

    /// <summary>At least one form; "unplaced" alone; every form allowed by the event type.</summary>
    private static RecordError? CheckForms(Place place, EventTypeDefinition type)
    {
        List<PlaceForm> forms = FormsOf(place);
        if (forms.Count == 0)
        {
            return new RecordError(ErrorCodes.PlaceMissing, "place",
                "a place needs a point, an admin area, a logical position or unplaced: true");
        }
        if (place.Unplaced != null && forms.Count > 1)
        {
            return new RecordError(ErrorCodes.PlaceConflict, "place.unplaced", "unplaced cannot be combined with a place");
        }
        foreach (PlaceForm form in forms)
        {
            if (!type.Place.Contains(form))
            {
                string formName = form.ToString().ToLowerInvariant();
                return new RecordError(ErrorCodes.PlaceForm, "place." + formName,
                    $"type '{type.Code}' does not allow a place of form '{formName}'");
            }
        }
        return null;
    }

    /// <summary>A point in range and with its uncertainty; an uncertainty only with a point.</summary>
    private static RecordError? CheckPoint(Place place)
    {
        if (place.Point == null)
        {
            if (place.UncertaintyKm != null)
            {
                return new RecordError(ErrorCodes.Schema, "place.uncertainty_km", "uncertainty_km belongs to a point");
            }
            return null;
        }
        double longitude = place.Point[0];
        double latitude = place.Point[1];
        if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90)
        {
            return new RecordError(ErrorCodes.Schema, "place.point", "longitude or latitude out of range");
        }
        if (place.UncertaintyKm == null)
        {
            return new RecordError(ErrorCodes.Schema, "place.uncertainty_km", "a point needs uncertainty_km");
        }
        return null;
    }

    /// <summary>A known country, and a subdivision that belongs to it.</summary>
    private static RecordError? CheckAdmin(AdminArea admin, RegistrySnapshot registry)
    {
        if (!registry.Countries.Contains(admin.Country))
        {
            return new RecordError(ErrorCodes.PlaceAdmin, "place.admin.country",
                $"'{admin.Country}' is not an ISO 3166-1 alpha-2 code");
        }
        if (admin.Subdivision != null && !admin.Subdivision.StartsWith(admin.Country + "-"))
        {
            return new RecordError(ErrorCodes.PlaceAdmin, "place.admin.subdivision",
                "the subdivision must belong to the country");
        }
        return null;
    }

    /// <summary>A registered space, a path within its depth, and a first value it accepts when it is closed.</summary>
    private static RecordError? CheckLogical(LogicalPlace logical, RegistrySnapshot registry)
    {
        if (!registry.LogicalSpaces.TryGetValue(logical.Space, out LogicalSpaceDefinition? space))
        {
            return new RecordError(ErrorCodes.LogicalSpace, "place.logical.space",
                $"logical space '{logical.Space}' is not registered");
        }
        if (logical.Path.Count > space.MaxDepth)
        {
            return new RecordError(ErrorCodes.LogicalDepth, "place.logical.path",
                $"'{logical.Space}' is at most {space.MaxDepth} deep");
        }
        if (space.Closed && !space.Values.Contains(logical.Path[0]))
        {
            return new RecordError(ErrorCodes.LogicalValue, "place.logical.path[0]",
                $"'{logical.Path[0]}' is not a registered value of '{logical.Space}'");
        }
        return null;
    }
}
