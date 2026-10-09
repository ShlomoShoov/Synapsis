using System.Text.Json.Nodes;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Tests.Support;
using Xunit;

namespace Synapsis.Sil.Tests.Validation.RecordRules;

/// <summary>Rule 8: the place.</summary>
public class PlaceRuleTests
{
    /// <summary>A place with no form at all is E-PLACE-MISSING.</summary>
    [Fact]
    public void A_place_without_a_form_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["place"] = new JsonObject { ["uncertainty_km"] = 5 };
        RuleCheck.Refused(record, ErrorCodes.PlaceMissing, "place");
    }

    /// <summary>"unplaced" together with a point is E-PLACE-CONFLICT.</summary>
    [Fact]
    public void Unplaced_with_a_point_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["place"]!["unplaced"] = true;
        RuleCheck.Refused(record, ErrorCodes.PlaceConflict, "place.unplaced");
    }

    /// <summary>A form the type does not allow (an earthquake allows only a point) is E-PLACE-FORM.</summary>
    [Fact]
    public void A_form_the_type_does_not_allow_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["place"]!["admin"] = new JsonObject { ["country"] = "JP" };
        RuleCheck.Refused(record, ErrorCodes.PlaceForm, "place.admin");
    }

    /// <summary>A latitude beyond 90 is E-SCHEMA at place.point.</summary>
    [Fact]
    public void A_point_out_of_range_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["place"]!["point"] = new JsonArray(10, 95);
        RuleCheck.Refused(record, ErrorCodes.Schema, "place.point");
    }

    /// <summary>A point without its uncertainty is E-SCHEMA at place.uncertainty_km.</summary>
    [Fact]
    public void A_point_without_uncertainty_is_refused()
    {
        JsonObject record = TestRecords.Earthquake();
        record["place"]!.AsObject().Remove("uncertainty_km");
        RuleCheck.Refused(record, ErrorCodes.Schema, "place.uncertainty_km");
    }

    /// <summary>An unknown country is E-PLACE-ADMIN; a subdivision of another country too.</summary>
    [Fact]
    public void Wrong_iso_codes_are_refused()
    {
        JsonObject unknownCountry = TestRecords.BankQuarter();
        unknownCountry["place"]!["admin"]!["country"] = "QQ";
        RuleCheck.Refused(unknownCountry, ErrorCodes.PlaceAdmin, "place.admin.country");

        JsonObject foreignSubdivision = TestRecords.BankQuarter();
        foreignSubdivision["place"]!["admin"]!["subdivision"] = "CA-ON";
        RuleCheck.Refused(foreignSubdivision, ErrorCodes.PlaceAdmin, "place.admin.subdivision");
    }

    /// <summary>An unknown logical space is E-LOGICAL-SPACE; a path deeper than allowed is E-LOGICAL-DEPTH.</summary>
    [Fact]
    public void Wrong_logical_places_are_refused()
    {
        JsonObject unknownSpace = TestRecords.KevEvent();
        unknownSpace["place"]!["logical"]!["space"] = "cyber.nowhere";
        RuleCheck.Refused(unknownSpace, ErrorCodes.LogicalSpace, "place.logical.space");

        JsonObject tooDeep = TestRecords.KevEvent();
        tooDeep["place"]!["logical"]!["path"] = new JsonArray("vendor", "product", "version");
        RuleCheck.Refused(tooDeep, ErrorCodes.LogicalDepth, "place.logical.path");
    }
}
