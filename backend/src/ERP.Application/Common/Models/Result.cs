namespace ERP.Application.Common.Models;

/// <summary>
/// Outcome of a use case. Avoids exceptions for expected business failures;
/// carries a stable machine-readable <see cref="Code"/> for the API error envelope.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public string? Code { get; }
    public string? Message { get; }

    protected Result(bool succeeded, string? code, string? message)
    {
        Succeeded = succeeded;
        Code = code;
        Message = message;
    }

    public static Result Ok() => new(true, null, null);
    public static Result Fail(string code, string message) => new(false, code, message);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, string? code, string? message)
        : base(succeeded, code, message)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, null, null);
    public static new Result<T> Fail(string code, string message) => new(false, default, code, message);
}
