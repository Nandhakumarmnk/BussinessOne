namespace ERP.Application.Common.Exceptions;

/// <summary>Field-level validation failure (mapped to HTTP 422 by the API).</summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

/// <summary>Caller is authenticated but lacks the required permission / business membership.</summary>
public class ForbiddenException : Exception
{
    public string Code { get; }

    public ForbiddenException(string code = "auth.forbidden", string message = "Forbidden.")
        : base(message)
    {
        Code = code;
    }
}

/// <summary>Requested resource was not found (or filtered out by tenant scope).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message = "Resource not found.") : base(message) { }
}
