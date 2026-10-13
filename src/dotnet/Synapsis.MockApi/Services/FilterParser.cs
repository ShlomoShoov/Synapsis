using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Synapsis.MockApi.Models.Requests;

namespace Synapsis.MockApi.Services;

/// <summary>
/// Reads the filter values that are text with a shape (the box, the measure conditions, the H3 cells) and finds the
/// mistakes in the filters. Pure code: used by the validation (400 answers) and by the code that builds the queries,
/// so a value is read the same way everywhere.
/// </summary>
public static class FilterParser
{
    /// <summary>The record kinds a kind filter may name.</summary>
    private static readonly HashSet<string> KindNames = new HashSet<string> { "entity", "event", "conclusion" };

    /// <summary>The comparisons a measure condition may use.</summary>
    private static readonly HashSet<string> Operators = new HashSet<string> { "gt", "gte", "lt", "lte", "eq" };

    /// <summary>Every mistake in the filters, each naming its query parameter; empty when they are fine.</summary>
    public static List<ValidationResult> Check(AnalyzeFilters filters)
    {
        List<ValidationResult> mistakes = new List<ValidationResult>();
        foreach (string kind in filters.Kinds)
        {
            if (!KindNames.Contains(kind))
            {
                mistakes.Add(Mistake("kind", "kind '" + kind + "' must be entity, event or conclusion."));
            }
        }
        if (filters.From != null && filters.To != null && filters.To.Value <= filters.From.Value)
        {
            mistakes.Add(Mistake("to", "to must be after from."));
        }
        if (filters.BoundingBox != null && ParseBoundingBox(filters.BoundingBox) == null)
        {
            mistakes.Add(Mistake("bbox", "bbox must be min_lon,min_lat,max_lon,max_lat: longitudes -180 to 180, latitudes -90 to 90, south not above north."));
        }
        foreach (string cell in filters.Cells)
        {
            if (CellResolution(cell) == null)
            {
                mistakes.Add(Mistake("cell", "cell '" + cell + "' is not an H3 cell of resolution 2 to 5."));
            }
        }
        foreach (string measure in filters.Measures)
        {
            if (ParseMeasure(measure) == null)
            {
                mistakes.Add(Mistake("measure", "measure '" + measure + "' must be name:op:value with op gt, gte, lt, lte or eq."));
            }
        }
        return mistakes;
    }

    /// <summary>The box read from "min_lon,min_lat,max_lon,max_lat"; null when it is not four numbers in range.</summary>
    public static BoundingBox? ParseBoundingBox(string text)
    {
        string[] parts = text.Split(',');
        if (parts.Length != 4)
        {
            return null;
        }
        double[] numbers = new double[4];
        for (int index = 0; index < 4; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }
        bool longitudesInRange = Math.Abs(numbers[0]) <= 180 && Math.Abs(numbers[2]) <= 180;
        bool latitudesInRange = Math.Abs(numbers[1]) <= 90 && Math.Abs(numbers[3]) <= 90 && numbers[1] <= numbers[3];
        if (!longitudesInRange || !latitudesInRange)
        {
            return null;
        }
        return new BoundingBox { MinLongitude = numbers[0], MinLatitude = numbers[1], MaxLongitude = numbers[2], MaxLatitude = numbers[3] };
    }

    /// <summary>The condition read from "name:op:value"; null when it does not have that shape.</summary>
    public static MeasureCondition? ParseMeasure(string text)
    {
        string[] parts = text.Split(':');
        if (parts.Length != 3 || parts[0].Length == 0 || !Operators.Contains(parts[1]))
        {
            return null;
        }
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return null;
        }
        return new MeasureCondition { Name = parts[0], Operator = parts[1], Value = value };
    }

    /// <summary>
    /// The resolution of an H3 cell id such as "832a10fffffffff" (the second character holds it), when it is a cell
    /// of resolution 2 to 5, the ones the index keeps; otherwise null.
    /// </summary>
    public static int? CellResolution(string cell)
    {
        if (cell.Length != 15 || cell[0] != '8')
        {
            return null;
        }
        foreach (char character in cell)
        {
            if (!Uri.IsHexDigit(character) || char.IsUpper(character))
            {
                return null;
            }
        }
        int resolution = Convert.ToInt32(cell.Substring(1, 1), 16);
        if (resolution < 2 || resolution > 5)
        {
            return null;
        }
        return resolution;
    }

    /// <summary>The moment in UTC: a date or time given without a zone is taken as UTC (a date alone is midnight UTC).</summary>
    public static DateTime Utc(DateTime moment)
    {
        if (moment.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(moment, DateTimeKind.Utc);
        }
        return moment.ToUniversalTime();
    }

    /// <summary>A mistake about one query parameter.</summary>
    private static ValidationResult Mistake(string parameter, string message)
    {
        return new ValidationResult(message, new[] { parameter });
    }
}
