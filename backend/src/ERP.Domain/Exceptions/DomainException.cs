namespace ERP.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant or business rule is violated.
/// Carries a stable machine-readable code (see docs/07 error catalogue).
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}
