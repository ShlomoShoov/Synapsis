using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Synapsis.Api.Services.Swagger;

/// <summary>
/// Adds a text body to the Swagger description of every action marked <see cref="RawBodyAttribute"/>, so the Swagger UI
/// lets you paste a batch or a package and send it.
/// </summary>
public sealed class RawBodyOperationFilter : IOperationFilter
{
    /// <summary>Adds the body when the action has the attribute; leaves other actions as they are.</summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        RawBodyAttribute? rawBody = context.MethodInfo.GetCustomAttribute<RawBodyAttribute>();
        if (rawBody == null)
        {
            return;
        }
        OpenApiRequestBody body = new OpenApiRequestBody { Required = true };
        body.Content[rawBody.ContentType] = new OpenApiMediaType { Schema = new OpenApiSchema { Type = "string" } };
        operation.RequestBody = body;
    }
}
