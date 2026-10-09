using System.Collections.Generic;
using Synapsis.Sil.Models.Records;
using Synapsis.Sil.Models.Records.Parts;
using Synapsis.Sil.Models.Results;
using Synapsis.Sil.Models.Vocabulary.Terms;
using Synapsis.Sil.Models.Vocabulary.Types;
using Synapsis.Sil.Registry.Abstractions;
using Synapsis.Sil.Validation.Abstractions;
using Synapsis.Sil.Validation.Models;

namespace Synapsis.Sil.Validation.Services.RecordRules;

/// <summary>
/// Rule 8 (events): the place must have a form, only forms its event type allows, a point in range with its
/// uncertainty, real ISO codes, and a registered logical position within its space's depth and closed values.
/// </summary>
public sealed class PlaceRule : IRecordRule
{
    /// <summary>Passes (always for other kinds), or fails with E-PLACE-*, E-LOGICAL-*, or E-SCHEMA for the point.</summary>
    public Result Check(RecordContext context)
    {
        if (context.Record is not EventRecord eventRecord)
        {
            return Result.Success();
        }
        Place place = eventRecord.Place;
        Result result = CheckForms(place, context.Registry.FindEventType(eventRecord.Type)!);
        if (result.Succeeded)
        {
            result = CheckPoint(place);
        }
        if (result.Succeeded && place.Admin != null)
        {
            result = CheckAdmin(place.Admin, context.Registry);
        }
        if (result.Succeeded && place.Logical != null)
        {
            result = CheckLogical(place.Logical, context.Registry);
        }
        return result;
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
    private static Result CheckForms(Place place, EventTypeDefinition type)
    {
        List<PlaceForm> forms = FormsOf(place);
        if (forms.Count == 0)
        {
            return Result.Failure(ErrorCodes.PlaceMissing, "place", "a place needs a point, an admin area, a logical position or unplaced: true");
        }
        if (place.Unplaced != null && forms.Count > 1)
        {
            return Result.Failure(ErrorCodes.PlaceConflict, "place.unplaced", "unplaced cannot be combined with a place");
        }
        foreach (PlaceForm form in forms)
        {
            if (!type.Place.Contains(form))
            {
                string formName = form.ToString().ToLowerInvariant();
                return Result.Failure(ErrorCodes.PlaceForm, "place." + formName, $"type '{type.Code}' does not allow a place of form '{formName}'");
            }
        }
        return Result.Success();
    }

    /// <summary>A point in range and with its uncertainty; an uncertainty only with a point.</summary>
    private static Result CheckPoint(Place place)
    {
        if (place.Point == null)
        {
            if (place.UncertaintyKm != null)
            {
                return Result.Failure(ErrorCodes.Schema, "place.uncertainty_km", "uncertainty_km belongs to a point");
            }
            return Result.Success();
        }
        double longitude = place.Point[0];
        double latitude = place.Point[1];
        if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90)
        {
            return Result.Failure(ErrorCodes.Schema, "place.point", "longitude or latitude out of range");
        }
        if (place.UncertaintyKm == null)
        {
            return Result.Failure(ErrorCodes.Schema, "place.uncertainty_km", "a point needs uncertainty_km");
        }
        return Result.Success();
    }

    /// <summary>A known country, and a subdivision that belongs to it.</summary>
    private static Result CheckAdmin(AdminArea admin, IRegistry registry)
    {
        if (!registry.IsCountry(admin.Country))
        {
            return Result.Failure(ErrorCodes.PlaceAdmin, "place.admin.country", $"'{admin.Country}' is not an ISO 3166-1 alpha-2 code");
        }
        if (admin.Subdivision != null && !admin.Subdivision.StartsWith(admin.Country + "-"))
        {
            return Result.Failure(ErrorCodes.PlaceAdmin, "place.admin.subdivision", "the subdivision must belong to the country");
        }
        return Result.Success();
    }

    /// <summary>A registered space, a path within its depth, and a first value it accepts when it is closed.</summary>
    private static Result CheckLogical(LogicalPlace logical, IRegistry registry)
    {
        LogicalSpaceDefinition? space = registry.FindLogicalSpace(logical.Space);
        if (space == null)
        {
            return Result.Failure(ErrorCodes.LogicalSpace, "place.logical.space", $"logical space '{logical.Space}' is not registered");
        }
        if (logical.Path.Count > space.MaxDepth)
        {
            return Result.Failure(ErrorCodes.LogicalDepth, "place.logical.path", $"'{logical.Space}' is at most {space.MaxDepth} deep");
        }
        if (space.Closed && !space.Values.Contains(logical.Path[0]))
        {
            return Result.Failure(ErrorCodes.LogicalValue, "place.logical.path[0]", $"'{logical.Path[0]}' is not a registered value of '{logical.Space}'");
        }
        return Result.Success();
    }
}
