namespace AshmesMarketplaces.API.DevelopmentSeed;

public static class DevelopmentSeedData
{
    public static readonly DateTime SeedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyCollection<SeedRole> Roles =
    [
        new("Admin", "Full local development access"),
        new("Manager", "Local development manager account"),
        new("Analyst", "Local development analyst account"),
        new("Viewer", "Local development read-oriented account")
    ];

    public static readonly IReadOnlyCollection<SeedUser> Users =
    [
        new("admin@ashmes.local", "Admin123!", "Admin", "+70000000001"),
        new("manager@ashmes.local", "Manager123!", "Manager", "+70000000002"),
        new("analyst@ashmes.local", "Analyst123!", "Analyst", "+70000000003"),
        new("viewer@ashmes.local", "Viewer123!", "Viewer", "+70000000004")
    ];

    public static readonly IReadOnlyCollection<SeedProduct> Products =
    [
        new("ASH-LOCAL-001", "WB-ASH-001", "Ashmes Premium Organizer", "4680000000001", 12, 2),
        new("ASH-LOCAL-002", "WB-ASH-002", "Ashmes Storage Box", "4680000000002", 15, 2),
        new("ASH-LOCAL-003", "WB-ASH-003", "Ashmes Travel Pouch", "4680000000003", 10, 1)
    ];

    public static readonly IReadOnlyCollection<SeedExpenseCategory> ExpenseCategories =
    [
        new("Marketplace Operations", "Операционные расходы для локальной демо-витрины."),
        new("Реклама", "Расходы на продвижение карточек и тестовые кампании."),
        new("Упаковка", "Короба, пакеты, маркировка и расходные материалы."),
        new("Логистика", "Доставка партий и операционные перевозки."),
        new("Фотоконтент", "Фотосъемка, ретушь и визуальные материалы для карточек."),
        new("Хранение", "Расходы на хранение товарных партий."),
        new("Сервис и инструменты", "Подписки и рабочие инструменты продавца."),
        new("Возвраты", "Обработка, проверка и переупаковка возвратов."),
        new("Сертификация", "Документы и сертификация товарных позиций."),
        new("Подрядчики", "Услуги внешних специалистов и подрядчиков.")
    ];

    public static readonly IReadOnlyCollection<SeedExpense> Expenses =
    [
        new(
            "Фотосъёмка карточки товара",
            "Фотоконтент",
            18_000m,
            Status: 2,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 30,
            DateUpdateOffsetDays: 31,
            DatePayOffsetDays: 21,
            "Съёмка основного фото и дополнительных ракурсов для карточки товара."),
        new(
            "Инфографика для WB карточки",
            "Фотоконтент",
            9_500m,
            Status: 2,
            ResponsibleRoleName: "Analyst",
            DateCreateOffsetDays: 31,
            DateUpdateOffsetDays: 32,
            DatePayOffsetDays: 22,
            "Дизайн преимуществ, размеров и комплектации для карточки."),
        new(
            "Тестовая рекламная кампания",
            "Реклама",
            15_000m,
            Status: 1,
            ResponsibleRoleName: "Analyst",
            DateCreateOffsetDays: 32,
            DateUpdateOffsetDays: 33,
            DatePayOffsetDays: 45,
            "Бюджет на проверку спроса и видимости товара в поиске."),
        new(
            "Продвижение в поиске WB",
            "Реклама",
            32_000m,
            Status: 0,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 33,
            DateUpdateOffsetDays: 33,
            DatePayOffsetDays: null,
            "Планируемый расход на продвижение выбранных карточек."),
        new(
            "Закупка коробов",
            "Упаковка",
            7_200m,
            Status: 2,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 34,
            DateUpdateOffsetDays: 35,
            DatePayOffsetDays: 24,
            "Короба для отгрузки партии товара на маркетплейс."),
        new(
            "Пакеты и защитная упаковка",
            "Упаковка",
            4_300m,
            Status: 2,
            ResponsibleRoleName: null,
            DateCreateOffsetDays: 35,
            DateUpdateOffsetDays: 36,
            DatePayOffsetDays: 25,
            "Пакеты, плёнка и защитные материалы для товара."),
        new(
            "Маркировка и стикеры",
            "Упаковка",
            2_800m,
            Status: 1,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 36,
            DateUpdateOffsetDays: 37,
            DatePayOffsetDays: 42,
            "Печать стикеров, маркировка и расходные материалы."),
        new(
            "Доставка партии до склада",
            "Логистика",
            12_600m,
            Status: 2,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 37,
            DateUpdateOffsetDays: 38,
            DatePayOffsetDays: 27,
            "Перевозка партии товара до пункта приёмки."),
        new(
            "Хранение товара",
            "Хранение",
            8_900m,
            Status: 1,
            ResponsibleRoleName: null,
            DateCreateOffsetDays: 38,
            DateUpdateOffsetDays: 38,
            DatePayOffsetDays: null,
            "Ожидаемый расход на хранение партии товара."),
        new(
            "Обработка возвратов",
            "Возвраты",
            5_200m,
            Status: 2,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 39,
            DateUpdateOffsetDays: 40,
            DatePayOffsetDays: 30,
            "Проверка, переупаковка и обработка возвращённых товаров."),
        new(
            "Сертификация товара",
            "Сертификация",
            24_000m,
            Status: 0,
            ResponsibleRoleName: "Admin",
            DateCreateOffsetDays: 40,
            DateUpdateOffsetDays: 40,
            DatePayOffsetDays: null,
            "Подготовка документов и сертификация новой позиции."),
        new(
            "Проверка качества партии",
            "Подрядчики",
            11_000m,
            Status: 2,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 41,
            DateUpdateOffsetDays: 42,
            DatePayOffsetDays: 34,
            "Выборочная проверка качества перед поставкой."),
        new(
            "Сервис аналитики маркетплейса",
            "Сервис и инструменты",
            6_900m,
            Status: 2,
            ResponsibleRoleName: "Analyst",
            DateCreateOffsetDays: 42,
            DateUpdateOffsetDays: 43,
            DatePayOffsetDays: 35,
            "Подписка на сервис для анализа карточек, цен и конкурентов."),
        new(
            "Услуги дизайнера",
            "Подрядчики",
            13_500m,
            Status: 1,
            ResponsibleRoleName: "Manager",
            DateCreateOffsetDays: 43,
            DateUpdateOffsetDays: 43,
            DatePayOffsetDays: null,
            "Подготовка визуальных материалов и баннеров для карточки."),
        new(
            "Отменённая фотосъёмка",
            "Фотоконтент",
            6_000m,
            Status: 3,
            ResponsibleRoleName: null,
            DateCreateOffsetDays: 44,
            DateUpdateOffsetDays: 45,
            DatePayOffsetDays: null,
            "Расход отменён после переноса запуска товара.")
    ];

    public sealed record SeedRole(string Name, string Description);
    public sealed record SeedUser(string Login, string Password, string RoleName, string Phone);
    public sealed record SeedProduct(
        string SkuSeller,
        string SkuProduct,
        string Name,
        string Barcode,
        int Commission,
        int Status);
    public sealed record SeedExpenseCategory(string Name, string Description);
    public sealed record SeedExpense(
        string Name,
        string CategoryName,
        decimal Cost,
        int Status,
        string? ResponsibleRoleName,
        int DateCreateOffsetDays,
        int DateUpdateOffsetDays,
        int? DatePayOffsetDays,
        string Description);
}
