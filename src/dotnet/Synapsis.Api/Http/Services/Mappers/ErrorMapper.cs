using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Synapsis.Api.Http.Models.Descriptions;
using Synapsis.Api.Http.Models.Dtos;
using Synapsis.Sil.Models.Descriptions;
using Synapsis.Sil.Models.Results;

namespace Synapsis.Api.Http.Services.Mappers;

/// <summary>Turns problems into the error body and its HTTP status: every refused request leaves the same way.</summary>
public static class ErrorMapper
{
    /// <summary>A problem as the error body; its path, when it has one, becomes the one detail.</summary>
    public static ErrorDto ToDto(Problem problem)
    {
        List<ErrorDetailDto>? details = null;
        if (problem.Path.Length > 0)
        {
            details = new List<ErrorDetailDto> { new ErrorDetailDto { Path = problem.Path, Message = problem.Message } };
        }
        return new ErrorDto { Code = problem.Code, Message = problem.Message, Details = details };
    }

    /// <summary>A problem as the answer to the request: its body with its status.</summary>
    public static ObjectResult ToResult(Problem problem)
    {
        return new ObjectResult(ToDto(problem)) { StatusCode = ProblemStatuses.Of(problem.Code) };
    }

    /// <summary>The answer when a query parameter cannot be read (for example dry_run=maybe): E-PARAM, with each parameter.</summary>
    public static IActionResult ToParameterResult(ActionContext context)
    {
        List<ErrorDetailDto> details = new List<ErrorDetailDto>();
        foreach (KeyValuePair<string, ModelStateEntry> entry in context.ModelState)
        {
            foreach (ModelError error in entry.Value.Errors)
            {
                details.Add(new ErrorDetailDto { Path = entry.Key, Message = error.ErrorMessage });
            }
        }
        ErrorDto body = new ErrorDto { Code = ErrorCodes.Param, Message = "a parameter is missing or invalid", Details = details };
        return new ObjectResult(body) { StatusCode = ProblemStatuses.Of(ErrorCodes.Param) };
    }
}
