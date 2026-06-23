using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

public record BatchSaleDto(
    Guid Id, Guid BatchId, DateOnly SaleDate, int SaleQuantity, decimal? TotalWeight, decimal SaleAmount, Guid? CustomerId);

[HasPermission(Permissions.Farm.BatchManage)]
public record GetBatchSalesQuery(Guid BatchId) : IRequest<IReadOnlyList<BatchSaleDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record AddBatchSaleCommand(Guid BatchId, DateOnly SaleDate, int SaleQuantity, decimal? TotalWeight, decimal SaleAmount, Guid? CustomerId)
    : IRequest<Result<BatchSaleDto>>;

public class AddBatchSaleCommandValidator : AbstractValidator<AddBatchSaleCommand>
{
    public AddBatchSaleCommandValidator()
    {
        RuleFor(x => x.SaleQuantity).GreaterThan(0);
        RuleFor(x => x.SaleAmount).GreaterThanOrEqualTo(0);
    }
}

public class GetBatchSalesQueryHandler : IRequestHandler<GetBatchSalesQuery, IReadOnlyList<BatchSaleDto>>
{
    private readonly IRepository<BatchSale> _repo;
    public GetBatchSalesQueryHandler(IRepository<BatchSale> repo) => _repo = repo;

    public async Task<IReadOnlyList<BatchSaleDto>> Handle(GetBatchSalesQuery request, CancellationToken ct)
        => await _repo.Query().Where(s => s.BatchId == request.BatchId).OrderByDescending(s => s.SaleDate)
            .Select(s => new BatchSaleDto(s.Id, s.BatchId, s.SaleDate, s.SaleQuantity, s.TotalWeight, s.SaleAmount, s.CustomerId))
            .ToListAsync(ct);
}

public class AddBatchSaleCommandHandler : IRequestHandler<AddBatchSaleCommand, Result<BatchSaleDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddBatchSaleCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<BatchSaleDto>> Handle(AddBatchSaleCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var batch = await _uow.Repository<FarmBatch>().GetByIdAsync(request.BatchId, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");

        var sale = new BatchSale
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            SaleDate = request.SaleDate,
            SaleQuantity = request.SaleQuantity,
            TotalWeight = request.TotalWeight,
            SaleAmount = request.SaleAmount,
            CustomerId = request.CustomerId
        };
        await _uow.Repository<BatchSale>().AddAsync(sale, ct);

        if (batch.Status == "active")
        {
            batch.Status = "sold";
            _uow.Repository<FarmBatch>().Update(batch);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<BatchSaleDto>.Ok(new BatchSaleDto(sale.Id, sale.BatchId, sale.SaleDate, sale.SaleQuantity, sale.TotalWeight, sale.SaleAmount, sale.CustomerId));
    }
}
