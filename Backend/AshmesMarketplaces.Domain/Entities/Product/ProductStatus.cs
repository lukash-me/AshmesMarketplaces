namespace AshmesMarketplaces.Domain.Entities.Product;

public enum ProductStatus
{
    Draft,          // создан, но не отправлен
    Pending,        // отправлен, ждёт модерации
    Active,         // продаётся
    Rejected,       // отклонён
    Blocked,        // заблокирован
    Archived,       // архив
    OutOfStock,      // нет остатков
    Disappeared
}