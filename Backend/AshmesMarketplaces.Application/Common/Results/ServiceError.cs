namespace AshmesMarketplaces.Application.Common.Results;

public enum ServiceErrorType
{
    BadRequest,
    NotFound,
    Conflict
}

public sealed record ServiceError(ServiceErrorType Type, string Message);
