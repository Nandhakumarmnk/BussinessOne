using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Coconut;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Coconut;

public record CoconutBatchDto(
    Guid Id, Guid ProductId, string? ProductName, string BatchNumber, DateOnly PurchaseDate,
    decimal Quantity, decimal PurchaseAmount, string Status);

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetCoconutBatchesQuery(string? Status) : IRequest<IReadOnlyList<CoconutBatchDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record CreateCoconutBatchCommand(
    Guid ProductId, string BatchNumber, DateOnly PurchaseDate, decimal Quantity, decimal PurchaseAmount)
    : IRequest<Result<CoconutBatchDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record UpdateCoconutBatchCommand(
    Guid Id, DateOnly PurchaseDate, decimal Quantity, decimal PurchaseAmount, string Status)
    : IRequest<Result<CoconutBatchDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record DeleteCoconutBatchCommand(Guid Id) : IRequest<Result>;

public class CreateCoconutBatchCommandValidator : AbstractValidator<CreateCoconutBatchCommand>
{
    public CreateCoconutBatchCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PurchaseAmount).GreaterThanOrEqualTo(0);
    }
}

public class GetCoconutBatchesQueryHandler : IRequestHandler<GetCoconutBatchesQuery, IReadOnlyList<CoconutBatchDto>>
{
    private readonly IRepository<CoconutBatch> _batches;
    private readonly IRepository<Product> _products;
    public GetCoconutBatchesQueryHandler(IRepository<CoconutBatch> batches, IRepository<Product> products)
    {
        _batches = batches;
        _products = products;
    }

    public async Task<IReadOnlyList<CoconutBatchDto>> Handle(GetCoconutBatchesQuery request, CancellationToken ct)
    {
        var products = _products;
        var q = _batches.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(b => b.Status == request.Status);
        return await q.OrderByDescending(b => b.PurchaseDate)
            .Select(b => new CoconutBatchDto(
                b.Id, b.ProductId,
                products.Query().Where(p => p.Id == b.ProductId).Select(p => p.Name).FirstOrDefault(),
                b.BatchNumber, b.PurchaseDate, b.Quantity, b.PurchaseAmount, b.Status))
            .ToListAsync(ct);
    }
}

public class CreateCoconutBatchCommandHandler : IRequestHandler<CreateCoconutBatchCommand, Result<CoconutBatchDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateCoconutBatchCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<CoconutBatchDto>> Handle(CreateCoconutBatchCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var product = await _uow.Repository<Product>().GetByIdAsync(request.ProductId, ct);
        if (product is null) return Result<CoconutBatchDto>.Fail("resource.not_found", "Product not found.");

        var number = request.BatchNumber.Trim();
        if (await _uow.Repository<CoconutBatch>().Query().AnyAsync(b => b.BatchNumber == number, ct))
            return Result<CoconutBatchDto>.Fail("resource.conflict", "A batch with that number already exists.");

        var batch = new CoconutBatch
        {
            BusinessId = businessId,
            ProductId = request.ProductId,
            BatchNumber = number,
            PurchaseDate = request.PurchaseDate,
            Quantity = request.Quantity,
            PurchaseAmount = request.PurchaseAmount
        };
        await _uow.Repository<CoconutBatch>().AddAsync(batch, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<CoconutBatchDto>.Ok(new CoconutBatchDto(
            batch.Id, batch.ProductId, product.Name, batch.BatchNumber, batch.PurchaseDate, batch.Quantity, batch.PurchaseAmount, batch.Status));
    }
}

public class UpdateCoconutBatchCommandHandler : IRequestHandler<UpdateCoconutBatchCommand, Result<CoconutBatchDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateCoconutBatchCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<CoconutBatchDto>> Handle(UpdateCoconutBatchCommand request, CancellationToken ct)
    {
        var batch = await _uow.Repository<CoconutBatch>().GetByIdAsync(request.Id, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");

        batch.PurchaseDate = request.PurchaseDate;
        batch.Quantity = request.Quantity;
        batch.PurchaseAmount = request.PurchaseAmount;
        batch.Status = request.Status;
        _uow.Repository<CoconutBatch>().Update(batch);
        await _uow.SaveChangesAsync(ct);

        var productName = await _uow.Repository<Product>().Query()
            .Where(p => p.Id == batch.ProductId).Select(p => p.Name).FirstOrDefaultAsync(ct);
        return Result<CoconutBatchDto>.Ok(new CoconutBatchDto(
            batch.Id, batch.ProductId, productName, batch.BatchNumber, batch.PurchaseDate, batch.Quantity, batch.PurchaseAmount, batch.Status));
    }
}

public class DeleteCoconutBatchCommandHandler : IRequestHandler<DeleteCoconutBatchCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteCoconutBatchCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteCoconutBatchCommand request, CancellationToken ct)
    {
        var batch = await _uow.Repository<CoconutBatch>().GetByIdAsync(request.Id, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");
        _uow.Repository<CoconutBatch>().Remove(batch);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
