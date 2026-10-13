namespace Synapsis.Api.Models.Dto;

/// <summary>An entity named in an answer: its id (to ask for its point) and what to show.</summary>
public sealed class EntityLabel
{
    /// <summary>The entity's id, such as "ent:gtd-group:left-wing-militants".</summary>
    public required string Id { get; init; }

    /// <summary>Its name, or its id when no name is known.</summary>
    public required string Label { get; init; }
}
