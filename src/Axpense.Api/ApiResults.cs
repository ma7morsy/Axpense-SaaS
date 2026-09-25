using Axpense.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace Axpense.Api;

/// <summary>
/// Maps service results to HTTP responses using the standard error envelope:
/// { "error": { "code", "message", "details" } }.
/// </summary>
public static class ApiResults
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Succeeded) return onSuccess(result.Value!);
        return controller.Error(result.Error!);
    }

    public static IActionResult Error(this ControllerBase controller, ServiceError error)
    {
        var status = error.Type switch
        {
            ServiceErrorType.NotFound => StatusCodes.Status404NotFound,
            ServiceErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return controller.StatusCode(status, new
        {
            error = new { code = error.Code, message = error.Message, details = error.Details ?? new Dictionary<string, string>() }
        });
    }
}
