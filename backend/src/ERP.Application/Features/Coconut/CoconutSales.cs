using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Coconut;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Coconut;

public record CoconutSaleDto(Guid Id, Guid BatchId, DateOnly SaleDate, decimal SaleQuantity, decimal SaleValue, Guid? CustomerId);

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetCoconutSalesQuery(Guid BatchId) : IRequest<IReadOnlyList<CoconutSaleDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record AddCoconutSaleCommand(Guid BatchId, DateOnly SaleDate, decimal SaleQuantity, decimal SaleValue, Guid? CustomerId)
    : IRequest<Result<CoconutSaleDto>>;

public class AddCoconutSaleCommandValidator : AbstractValidator<AddCoconutSaleCommand>
{
    public AddCoconutSaleCommandValidator()
    {
        RuleFor(x => x.SaleQuantity).GreaterThan(0);
        RuleFor(x => x.SaleValue).GreaterThanOrEqualTo(0);
    }
}

public class GetCoconutSalesQueryHandler : IRequestHandler<GetCoconutSalesQuery, IReadOnlyList<CoconutSaleDto>>
{
    private readonly IRepository<CoconutBatchSale> _repo;
    public GetCoconutSalesQueryHandler(IRepository<CoconutBatchSale> repo) => _repo = repo;

    public async Task<IReadOnlyList<CoconutSaleDto>> Handle(GetCoconutSalesQuery request, CancellationToken ct)
        => await _repo.Query().Where(s => s.BatchId == request.BatchId).OrderByDescending(s => s.SaleDate)
            .Select(s => new CoconutSaleDto(s.Id, s.BatchId, s.SaleDate, s.SaleQuantity, s.SaleValue, s.CustomerId)).ToListAsync(ct);
}

public class AddCoconutSaleCommandHandler : IRequestHandler<AddCoconutSaleCommand, Result<CoconutSaleDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddCoconutSaleCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<CoconutSaleDto>> Handle(AddCoconutSaleCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var batch = await _uow.Repository<CoconutBatch>().GetByIdAsync(request.BatchId, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");

        var sale = new CoconutBatchSale
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            SaleDate = request.SaleDate,
            SaleQuantity = request.SaleQuantity,
            SaleValue = request.SaleValue,
            CustomerId = request.CustomerId
        };
        await _uow.Repository<CoconutBatchSale>().AddAsync(sale, ct);

        if (batch.Status == "active")
        {
            batch.Status = "sold";
            _uow.Repository<CoconutBatch>().Update(batch);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<CoconutSaleDto>.Ok(new CoconutSaleDto(sale.Id, sale.BatchId, sale.SaleDate, sale.SaleQuantity, sale.SaleValue, sale.CustomerId));
    }
}
