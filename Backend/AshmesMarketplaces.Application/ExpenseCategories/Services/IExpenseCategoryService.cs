using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.ExpenseCategories.Dtos;

namespace AshmesMarketplaces.Application.ExpenseCategories.Services;

public interface IExpenseCategoryService
{
    Task<ServiceResult<PagedResponse<ExpenseCategoryListItemResponse>>> GetListAsync(ExpenseCategoryListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseCategoryResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseCategoryResponse>> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseCategoryResponse>> UpdateAsync(Guid id, UpdateExpenseCategoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
