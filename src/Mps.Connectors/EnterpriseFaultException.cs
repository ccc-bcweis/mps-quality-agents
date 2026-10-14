// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

namespace Mps.Connectors;

public sealed class EnterpriseFaultException : Exception
{
    public EnterpriseFaultException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
