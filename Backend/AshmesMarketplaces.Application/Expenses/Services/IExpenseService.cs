using AshmesMarketplaces.Application.Common.Pagination;
using AshmesMarketplaces.Application.Common.Results;
using AshmesMarketplaces.Application.Expenses.Dtos;

namespace AshmesMarketplaces.Application.Expenses.Services;

public interface IExpenseService
{
    Task<ServiceResult<PagedResponse<ExpenseListItemResponse>>> GetListAsync(ExpenseListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseSummaryResponse>> GetSummaryAsync(ExpenseListQuery query, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<ExpenseResponse>> UpdateAsync(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
