using System;

namespace Synapsis.Api.Services.Swagger;

/// <summary>
/// Marks an action that reads its request body itself (as text) instead of through model binding, so Swagger can still
/// show a body box for it. Example: [RawBody("application/json")] on POST /ingest.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RawBodyAttribute : Attribute
{
    /// <summary>Marks the action with the content type its body is written in.</summary>
    public RawBodyAttribute(string contentType)
    {
        ContentType = contentType;
    }

    /// <summary>The content type of the body, such as "application/json" or "application/yaml".</summary>
    public string ContentType { get; }
}
