namespace FactuTrust.Domain.Common;

/// <summary>
/// Represents the result of an operation that can fail.
/// Follows the Result pattern for explicit error handling.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Cannot have error with success result");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Must have error with failure result");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default!, false, error);
}

/// <summary>
/// Represents the result of an operation that returns a value and can fail.
/// </summary>
public class Result<T> : Result
{
    private readonly T _value;

    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access value of a failed result");
            return _value;
        }
    }

    protected internal Result(T value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}

/// <summary>
/// Represents an error with a code and description.
/// </summary>
public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string entity, Guid id) =>
        new($"{entity}.NotFound", $"{entity} with ID '{id}' was not found.");

    public static Error Validation(string field, string message) =>
        new($"Validation.{field}", message);

    public static Error Conflict(string message) =>
        new("Conflict", message);

    public static Error Unauthorized(string message = "Unauthorized access") =>
        new("Unauthorized", message);

    public static Error Forbidden(string message = "Access denied") =>
        new("Forbidden", message);
}
