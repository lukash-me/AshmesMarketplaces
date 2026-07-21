namespace AshmesMarketplaces.Application.Common.Results;

public enum ServiceErrorType
{
    BadRequest,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    Unavailable
}

public sealed record ServiceError(ServiceErrorType Type, string Message);
