// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Domain;

/// <summary>
/// A domain invariant failed. Callers map this to a validation or conflict response.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message)
        : base(message)
    {
    }

    public DomainRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
