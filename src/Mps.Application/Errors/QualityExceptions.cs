// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Application.Errors;

public sealed class IntakeValidationException : Exception
{
    public IntakeValidationException(string message)
        : base(message)
    {
    }
}

public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class LifecycleConflictException : Exception
{
    public LifecycleConflictException(string message)
        : base(message)
    {
    }

    public LifecycleConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class CommandDeniedException : Exception
{
    public CommandDeniedException(string message)
        : base(message)
    {
    }
}

public sealed class ApprovalForbiddenException : Exception
{
    public ApprovalForbiddenException(string message)
        : base(message)
    {
    }

    public ApprovalForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A QMS write failed. Status 500 and 408 are retried. Status 409 with an existing id is success.
/// </summary>
public sealed class ConnectorWriteException : Exception
{
    public ConnectorWriteException(int statusCode, string? existingExternalId, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ExistingExternalId = string.IsNullOrWhiteSpace(existingExternalId) ? null : existingExternalId.Trim();
    }

    public int StatusCode { get; }

    public string? ExistingExternalId { get; }

    public bool IsRetryable => StatusCode is 500 or 408;

    public bool IsIdempotentConflict => StatusCode == 409 && ExistingExternalId is not null;
}
