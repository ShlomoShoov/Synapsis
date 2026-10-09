namespace Synapsis.Api.Ingestion.Models.Results;

/// <summary>A batch's outcome in one word.</summary>
public enum ReceiptStatus
{
    /// <summary>Every record was accepted, and something new was stored.</summary>
    Accepted,

    /// <summary>Some records were refused, the others accepted.</summary>
    Partial,

    /// <summary>Every record was refused.</summary>
    Rejected,

    /// <summary>Every record was accepted, and none brought anything new.</summary>
    Unchanged,
}
