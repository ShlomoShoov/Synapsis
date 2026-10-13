using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Synapsis.Api.Models.Requests;
using Xunit;

namespace Synapsis.Tests.Unit;

/// <summary>Checks the existing graph request validation used by the endpoint.</summary>
public sealed class GraphQueryRequestTests
{
    /// <summary>A missing graph id is rejected by the existing required rule.</summary>
    [Fact]
    public void Missing_id_is_rejected()
    {
        GraphRequest request = new GraphRequest();

        List<ValidationResult> errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(GraphRequest.Id)));
    }

    /// <summary>Limits below one or above two hundred are rejected by the existing range rule.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void Limit_outside_the_existing_range_is_rejected(int limit)
    {
        GraphRequest request = new GraphRequest { Id = "ent:test:center", Limit = limit };

        List<ValidationResult> errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(GraphRequest.Limit)));
    }

    /// <summary>Limits at both ends of the existing range are accepted.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Limit_at_the_existing_bounds_is_accepted(int limit)
    {
        GraphRequest request = new GraphRequest { Id = "ent:test:center", Limit = limit };

        List<ValidationResult> errors = Validate(request);

        Assert.Empty(errors);
    }

    /// <summary>An invalid existing graph filter is rejected by the shared filter validation.</summary>
    [Fact]
    public void Invalid_filter_is_rejected()
    {
        GraphRequest request = new GraphRequest
        {
            Id = "ent:test:center",
            Kinds = new List<string> { "unknown" }
        };

        List<ValidationResult> errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains("kind"));
    }

    /// <summary>Runs the same data annotation checks used for query request properties.</summary>
    private static List<ValidationResult> Validate(GraphRequest request)
    {
        List<ValidationResult> errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
        return errors;
    }
}
