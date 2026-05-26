namespace AshmesMarketplaces.Application.Common.Results;

public class ServiceResult
{
    protected ServiceResult(ServiceError? error)
    {
        Error = error;
    }

    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    public static ServiceResult Success() => new(null);

    public static ServiceResult BadRequest(string message) =>
        new(new ServiceError(ServiceErrorType.BadRequest, message));

    public static ServiceResult NotFound(string message) =>
        new(new ServiceError(ServiceErrorType.NotFound, message));

    public static ServiceResult Conflict(string message) =>
        new(new ServiceError(ServiceErrorType.Conflict, message));

    public static ServiceResult Unauthorized(string message) =>
        new(new ServiceError(ServiceErrorType.Unauthorized, message));

    public static ServiceResult Unavailable(string message) =>
        new(new ServiceError(ServiceErrorType.Unavailable, message));
}

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(T? value, ServiceError? error) : base(error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static ServiceResult<T> Success(T value) => new(value, null);

    public new static ServiceResult<T> BadRequest(string message) =>
        new(default, new ServiceError(ServiceErrorType.BadRequest, message));

    public new static ServiceResult<T> NotFound(string message) =>
        new(default, new ServiceError(ServiceErrorType.NotFound, message));

    public new static ServiceResult<T> Conflict(string message) =>
        new(default, new ServiceError(ServiceErrorType.Conflict, message));

    public new static ServiceResult<T> Unauthorized(string message) =>
        new(default, new ServiceError(ServiceErrorType.Unauthorized, message));

    public new static ServiceResult<T> Unavailable(string message) =>
        new(default, new ServiceError(ServiceErrorType.Unavailable, message));
}
