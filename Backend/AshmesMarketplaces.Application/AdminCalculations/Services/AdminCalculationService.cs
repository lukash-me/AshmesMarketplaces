using AshmesMarketplaces.Application.AdminCalculations.Dtos;
using AshmesMarketplaces.Application.Auth.Security;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.DataAccess;
using AshmesMarketplaces.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AshmesMarketplaces.Application.AdminCalculations.Services;

public sealed class AdminCalculationService : IAdminCalculationService
{
    private const string AdminRoleName = "Admin";
    private const string ScheduledExecutionMode = "scheduled";
    private const string OnDemandExecutionMode = "on_demand";

    private static readonly ISet<string> HiddenScheduleKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        PublicAnalysisSchedule.HotProductsScheduleKey
    };

    private static readonly ISet<string> ManuallyRunnableScheduleKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        PublicAnalysisSchedule.MarketConcentrationScheduleKey,
        PublicAnalysisSchedule.MarketIntelligenceScheduleKey,
        PublicAnalysisSchedule.MarketLogisticsEventsScheduleKey,
        PublicAnalysisSchedule.ProductAvailabilityScheduleKey,
        PublicAnalysisSchedule.ParserCurrentProductsScheduleKey
    };

    private static readonly IReadOnlyList<AdminCalculationScenarioDto> DefaultScenarios =
    [
        new(
            "standard_calculation",
            "Стандартный расчет",
            "Расчет выполняется по текущим данным сервиса. Ручной запуск использует ту же логику, что и плановый запуск, и не меняет время следующего запуска по расписанию.")
    ];

    private static readonly IReadOnlyList<AdminCalculationScenarioDto> ProductAvailabilityScenarios =
    [
        new(
            "marketplace_card_missing",
            "Карточка пропала с маркетплейса",
            "Если карточка не найдена в актуальном parser-наблюдении или помечена неактивной, обычная доступность не рассчитывается. Карточка исключается из витрины доступности, потому что старые остатки и логистика могут быть недостоверны. Такой случай должен анализироваться отдельной логикой исчезновения карточек с маркетплейса.")
    ];

    private static readonly IReadOnlyDictionary<string, CalculationDescriptor> Descriptors =
        new Dictionary<string, CalculationDescriptor>(StringComparer.Ordinal)
        {
            [PublicAnalysisSchedule.ParserCurrentProductsScheduleKey] = new(
                "Текущие карточки parser-а",
                "Сопоставляет полученные ранее позиции Top 700 маркетплейса с существующими карточками сервиса.",
                "Расчет не обращается к WB и не инициирует parser-запуск. Он пересобирает серверную current-витрину карточек по уже загруженным товарам и rank-данным.",
                true,
                OnDemandExecutionMode,
                DefaultScenarios),
            [PublicAnalysisSchedule.MarketIntelligenceScheduleKey] = new(
                "Карта цены и качества",
                "Обновляет публичную рыночную аналитику по цене и качеству.",
                "Расчет обновляет снимки для страницы аналитики рынка.",
                false,
                ScheduledExecutionMode,
                DefaultScenarios),
            [PublicAnalysisSchedule.MarketLogisticsEventsScheduleKey] = new(
                "Новые карточки",
                "Пересчитывает витрину для вкладки «Логистика и спрос -> Новые карточки».",
                "Ручной запуск выполняет тот же расчет, что и расписание: обновляет новые карточки, пополнения и уменьшения остатков по parser-данным. Плановое время следующего запуска при этом не меняется.",
                true,
                ScheduledExecutionMode,
                DefaultScenarios),
            [PublicAnalysisSchedule.ProductAvailabilityScheduleKey] = new(
                "Доступность товара",
                "Обновляет публичный снимок доступности товаров.",
                "Ручной запуск выполняет тот же расчет, что и расписание: обновляет публичный snapshot для страницы «Доступность товара». Плановое время следующего запуска при этом не меняется.",
                true,
                ScheduledExecutionMode,
                ProductAvailabilityScenarios),
            [PublicAnalysisSchedule.MarketConcentrationScheduleKey] = new(
                "Концентрация рынка",
                "Обновляет публичную аналитику концентрации рынка.",
                "Расчет используется страницей концентрации рынка.",
                false,
                ScheduledExecutionMode,
                DefaultScenarios),
            [PublicAnalysisSchedule.TopForecastScheduleKey] = new(
                "Прогноз топа",
                "Обновляет публичный прогноз позиций в топе.",
                "Расчет используется страницей прогноза топа.",
                false,
                ScheduledExecutionMode,
                DefaultScenarios),
            [PublicAnalysisSchedule.WildberriesCategoryCatalogScheduleKey] = new(
                "Справочник ниш WB",
                "Обновляет сохраненный справочник leaf-ниш Wildberries.",
                "Расчет обновляет категории для выбора ниши proxy в админке.",
                false,
                ScheduledExecutionMode,
                DefaultScenarios)
        };

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AdminCalculationService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<AdminCalculationDto>>> GetCalculationsAsync(CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<IReadOnlyList<AdminCalculationDto>>(access.Error!);

        var schedules = (await _dbContext.PublicAnalysisSchedules
            .AsNoTracking()
            .OrderBy(x => x.ScheduleKey)
            .ToListAsync(cancellationToken))
            .Where(x => !HiddenScheduleKeys.Contains(x.ScheduleKey))
            .ToList();

        var manualRunRows = await _dbContext.PublicAnalysisManualRunRequests
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        var manualBySchedule = manualRunRows
            .GroupBy(x => x.ScheduleKey, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var result = schedules
            .Select(schedule =>
            {
                var descriptor = ResolveAdminDescriptor(schedule.ScheduleKey);
                manualBySchedule.TryGetValue(schedule.ScheduleKey, out var manualRun);
                return new AdminCalculationDto(
                    schedule.ScheduleKey,
                    descriptor.ExecutionMode,
                    descriptor.Name,
                    descriptor.Description,
                    descriptor.Details,
                    descriptor.CanRunManually,
                    descriptor.CanRunManually ? null : "Ручной запуск будет добавлен на отдельном этапе.",
                    schedule.TimezoneId,
                    schedule.LocalTime.ToString("HH:mm"),
                    schedule.NextRunAtUtc,
                    schedule.LastStatus,
                    schedule.LastStartedAtUtc,
                    schedule.LastCompletedAtUtc,
                    schedule.LastError,
                    descriptor.Scenarios,
                    manualRun is null ? null : Map(manualRun));
            })
            .OrderBy(x => x.ExecutionMode, StringComparer.Ordinal)
            .ThenByDescending(x => x.CanRunManually)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        return ServiceResult<IReadOnlyList<AdminCalculationDto>>.Success(result);
    }

    public async Task<ServiceResult<AdminCalculationManualRunDto>> RequestManualRunAsync(
        string scheduleKey,
        CancellationToken cancellationToken)
    {
        var access = await EnsureAdminAsync(cancellationToken);
        if (!access.IsSuccess)
            return AccessError<AdminCalculationManualRunDto>(access.Error!);

        if (_currentUser.UserId is null)
            return ServiceResult<AdminCalculationManualRunDto>.Unauthorized("Authentication is required.");

        var normalizedScheduleKey = string.IsNullOrWhiteSpace(scheduleKey) ? string.Empty : scheduleKey.Trim();
        if (!ManuallyRunnableScheduleKeys.Contains(normalizedScheduleKey))
        {
            return ServiceResult<AdminCalculationManualRunDto>.BadRequest(
                "Вручную сейчас можно запустить только расчеты «Карта цены и качества», «Концентрация рынка», «Новые карточки», «Доступность товара» и «Текущие карточки parser-а».");
        }

        var scheduleExists = await _dbContext.PublicAnalysisSchedules
            .AsNoTracking()
            .AnyAsync(x => x.ScheduleKey == normalizedScheduleKey, cancellationToken);
        if (!scheduleExists)
            return ServiceResult<AdminCalculationManualRunDto>.NotFound("Расписание расчета не найдено.");

        var hasActiveRun = await _dbContext.PublicAnalysisManualRunRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.ScheduleKey == normalizedScheduleKey &&
                     PublicAnalysisManualRunRequest.ActiveStatuses.Contains(x.Status),
                cancellationToken);
        if (hasActiveRun)
            return ServiceResult<AdminCalculationManualRunDto>.Conflict("Этот расчет уже ожидает выполнения или выполняется.");

        var nowUtc = DateTime.UtcNow;
        var request = new PublicAnalysisManualRunRequest(normalizedScheduleKey, _currentUser.UserId.Value, nowUtc);
        _dbContext.PublicAnalysisManualRunRequests.Add(request);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<AdminCalculationManualRunDto>.Conflict("Этот расчет уже ожидает выполнения или выполняется.");
        }

        return ServiceResult<AdminCalculationManualRunDto>.Success(Map(request));
    }

    private async Task<ServiceResult> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.RoleId is null)
            return ServiceResult.Unauthorized("Authentication is required.");

        var roleName = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.RoleId)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken);

        return string.Equals(roleName, AdminRoleName, StringComparison.OrdinalIgnoreCase)
            ? ServiceResult.Success()
            : ServiceResult.Forbidden("Admin role is required.");
    }

    private static CalculationDescriptor ResolveAdminDescriptor(string scheduleKey)
    {
        if (string.Equals(scheduleKey, PublicAnalysisSchedule.MarketIntelligenceScheduleKey, StringComparison.Ordinal))
        {
            return new CalculationDescriptor(
                "Карта цены и качества",
                "Обновляет публичную рыночную аналитику по цене и качеству.",
                "Ручной запуск пересчитывает публичные snapshot-ы карты цены и качества тем же механизмом, что и расписание. Плановое время следующего запуска при этом не меняется.",
                true,
                ScheduledExecutionMode,
                DefaultScenarios);
        }

        if (string.Equals(scheduleKey, PublicAnalysisSchedule.MarketConcentrationScheduleKey, StringComparison.Ordinal))
        {
            return new CalculationDescriptor(
                "Концентрация рынка",
                "Обновляет публичную аналитику концентрации рынка.",
                "Ручной запуск пересчитывает публичные snapshot-ы концентрации рынка тем же механизмом, что и расписание. Плановое время следующего запуска при этом не меняется.",
                true,
                ScheduledExecutionMode,
                DefaultScenarios);
        }

        return ResolveDescriptor(scheduleKey);
    }

    private static CalculationDescriptor ResolveDescriptor(string scheduleKey) =>
        Descriptors.TryGetValue(scheduleKey, out var descriptor)
            ? descriptor
            : new CalculationDescriptor(
                scheduleKey,
                "Публичный расчет сервиса.",
                "Расписание создано в БД, но описание для него еще не добавлено.",
                false,
                ScheduledExecutionMode,
                DefaultScenarios);

    private static AdminCalculationManualRunDto Map(PublicAnalysisManualRunRequest request) =>
        new(
            request.Id,
            request.ScheduleKey,
            request.Status,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.StartedAtUtc,
            request.CompletedAtUtc,
            request.Error);

    private static ServiceResult<T> AccessError<T>(ServiceError error)
    {
        return error.Type switch
        {
            ServiceErrorType.Unauthorized => ServiceResult<T>.Unauthorized(error.Message),
            ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(error.Message),
            _ => ServiceResult<T>.Forbidden(error.Message)
        };
    }

    private sealed record CalculationDescriptor(
        string Name,
        string Description,
        string Details,
        bool CanRunManually,
        string ExecutionMode,
        IReadOnlyList<AdminCalculationScenarioDto> Scenarios);
}
