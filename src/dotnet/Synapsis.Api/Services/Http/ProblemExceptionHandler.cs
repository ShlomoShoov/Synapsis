using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Synapsis.Api.Models.Configuration;
using Synapsis.Api.Services.Http.Mappers;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Services.Http;

/// <summary>
/// Turns the failures the framework and the database raise as exceptions into the contract's answers:
/// a body sent as gzip that is not gzip → E-BUNDLE (400); a body over the size limit → E-TOO-LARGE (413);
/// MongoDB not answering → E-UNAVAILABLE (503). Any other exception is a bug and is left to the framework (500).
/// </summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemExceptionHandler> _log;

    /// <summary>Creates the handler.</summary>
    public ProblemExceptionHandler(ILogger<ProblemExceptionHandler> log)
    {
        _log = log;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Problem? problem = ProblemOf(exception);
        if (problem == null)
        {
            return false;
        }
        _log.LogWarning("{Code}: {Message}", problem.Code, exception.Message);
        httpContext.Response.StatusCode = ProblemStatuses.Of(problem.Code);
        await httpContext.Response.WriteAsJsonAsync(ErrorMapper.ToDto(problem), ApiJson.Options, cancellationToken);
        return true;
    }

    /// <summary>The contract's problem for this exception, or null when it is not one the contract names.</summary>
    private static Problem? ProblemOf(Exception exception)
    {
        if (exception is InvalidDataException)
        {
            return new Problem(ErrorCodes.Bundle, string.Empty, "the body is announced as gzip but is not gzip");
        }
        if (exception is BadHttpRequestException badRequest && badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new Problem(ErrorCodes.TooLarge, string.Empty, "the body is larger than the limit");
        }
        if (exception is MongoException || exception is TimeoutException)
        {
            return new Problem(ErrorCodes.Unavailable, string.Empty, "the database did not answer; send again");
        }
        return null;
    }
}
