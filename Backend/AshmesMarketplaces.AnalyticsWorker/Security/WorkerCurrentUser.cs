using AshmesMarketplaces.Application.Auth.Security;

namespace AshmesMarketplaces.AnalyticsWorker.Security;

public sealed class WorkerCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public Guid? RoleId => null;
    public int? SessionId => null;
}
