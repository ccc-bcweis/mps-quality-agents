// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.AspNetCore.Diagnostics;

using Mps.Application.Errors;
using Mps.Domain;

namespace Mps.Api.Http;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        var mapped = Map(exception);
        if (mapped is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = mapped.Value.Status;
        await Results.Problem(title: mapped.Value.Title, detail: exception.Message, statusCode: mapped.Value.Status)
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);
        return true;
    }

    private static (int Status, string Title)? Map(Exception exception) => exception switch
    {
        IntakeValidationException => (StatusCodes.Status400BadRequest, "The request failed validation."),
        ResourceNotFoundException => (StatusCodes.Status404NotFound, "No such record."),
        LifecycleConflictException => (StatusCodes.Status409Conflict, "The lifecycle rejected the transition."),
        ApprovalForbiddenException => (StatusCodes.Status403Forbidden, "Approval rejected by the token rules."),
        CommandDeniedException => (StatusCodes.Status403Forbidden, "The command is not allowed for this caller."),
        DomainRuleException => (StatusCodes.Status400BadRequest, "The request failed validation."),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request failed validation."),
        _ => null,
    };
}
