using System.Collections.Generic;
using Synapsis.Sil.Models.Descriptions;

namespace Synapsis.Api.Http.Models.Descriptions;

/// <summary>
/// The HTTP status of each error code that can answer a whole request. Record codes never become a status: they are
/// listed in the receipt, which answers 200.
/// </summary>
public static class ProblemStatuses
{
    /// <summary>The status of every request-level code.</summary>
    private static readonly Dictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        [ErrorCodes.Bundle] = 400,
        [ErrorCodes.Param] = 400,
        [ErrorCodes.Auth] = 401,
        [ErrorCodes.NotFound] = 404,
        [ErrorCodes.SchemaOwnership] = 409,
        [ErrorCodes.SchemaBreaking] = 409,
        [ErrorCodes.SchemaVersion] = 409,
        [ErrorCodes.TooLarge] = 413,
        [ErrorCodes.SchemaPackage] = 422,
        [ErrorCodes.SchemaRef] = 422,
        [ErrorCodes.Unavailable] = 503,
    };

    /// <summary>The status of a code; 500 for a code that should never answer a request.</summary>
    public static int Of(string code)
    {
        if (StatusByCode.TryGetValue(code, out int status))
        {
            return status;
        }
        return 500;
    }
}
