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
