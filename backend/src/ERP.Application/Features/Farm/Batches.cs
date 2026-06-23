using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

public record BatchDto(
    Guid Id, string BatchNumber, string? BatchName, string AnimalType, DateOnly StartDate,
    int QuantityPurchased, decimal PurchaseAmount, string Status);

[HasPermission(Permissions.Farm.BatchManage)]
public record GetBatchesQuery(string? Status) : IRequest<IReadOnlyList<BatchDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record CreateBatchCommand(
    string BatchNumber, string? BatchName, string AnimalType, DateOnly StartDate,
    int QuantityPurchased, decimal PurchaseAmount) : IRequest<Result<BatchDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record UpdateBatchCommand(
    Guid Id, string? BatchName, string AnimalType, DateOnly StartDate,
    int QuantityPurchased, decimal PurchaseAmount, string Status) : IRequest<Result<BatchDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record DeleteBatchCommand(Guid Id) : IRequest<Result>;

public class CreateBatchCommandValidator : AbstractValidator<CreateBatchCommand>
{
    public CreateBatchCommandValidator()
    {
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AnimalType).Must(a => AnimalTypes.All.Contains(a)).WithMessage("Animal type must be goat, hen or cow.");
        RuleFor(x => x.QuantityPurchased).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PurchaseAmount).GreaterThanOrEqualTo(0);
    }
}

public class UpdateBatchCommandValidator : AbstractValidator<UpdateBatchCommand>
{
    public UpdateBatchCommandValidator()
    {
        RuleFor(x => x.AnimalType).Must(a => AnimalTypes.All.Contains(a)).WithMessage("Animal type must be goat, hen or cow.");
        RuleFor(x => x.Status).NotEmpty();
    }
}

internal static class BatchMap
{
    public static BatchDto ToDto(FarmBatch b) => new(
        b.Id, b.BatchNumber, b.BatchName, b.AnimalType, b.StartDate, b.QuantityPurchased, b.PurchaseAmount, b.Status);
}

public class GetBatchesQueryHandler : IRequestHandler<GetBatchesQuery, IReadOnlyList<BatchDto>>
{
    private readonly IRepository<FarmBatch> _repo;
    public GetBatchesQueryHandler(IRepository<FarmBatch> repo) => _repo = repo;

    public async Task<IReadOnlyList<BatchDto>> Handle(GetBatchesQuery request, CancellationToken ct)
    {
        var q = _repo.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(b => b.Status == request.Status);
        return await q.OrderByDescending(b => b.StartDate)
            .Select(b => new BatchDto(b.Id, b.BatchNumber, b.BatchName, b.AnimalType, b.StartDate,
                b.QuantityPurchased, b.PurchaseAmount, b.Status))
            .ToListAsync(ct);
    }
}

public class CreateBatchCommandHandler : IRequestHandler<CreateBatchCommand, Result<BatchDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateBatchCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<BatchDto>> Handle(CreateBatchCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var number = request.BatchNumber.Trim();
        if (await _uow.Repository<FarmBatch>().Query().AnyAsync(b => b.BatchNumber == number, ct))
            return Result<BatchDto>.Fail("resource.conflict", "A batch with that number already exists.");

        var batch = new FarmBatch
        {
            BusinessId = businessId,
            BatchNumber = number,
            BatchName = request.BatchName,
            AnimalType = request.AnimalType,
            StartDate = request.StartDate,
            QuantityPurchased = request.QuantityPurchased,
            PurchaseAmount = request.PurchaseAmount
        };
        await _uow.Repository<FarmBatch>().AddAsync(batch, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<BatchDto>.Ok(BatchMap.ToDto(batch));
    }
}

public class UpdateBatchCommandHandler : IRequestHandler<UpdateBatchCommand, Result<BatchDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateBatchCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<BatchDto>> Handle(UpdateBatchCommand request, CancellationToken ct)
    {
        var batch = await _uow.Repository<FarmBatch>().GetByIdAsync(request.Id, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");

        batch.BatchName = request.BatchName;
        batch.AnimalType = request.AnimalType;
        batch.StartDate = request.StartDate;
        batch.QuantityPurchased = request.QuantityPurchased;
        batch.PurchaseAmount = request.PurchaseAmount;
        batch.Status = request.Status;
        _uow.Repository<FarmBatch>().Update(batch);
        await _uow.SaveChangesAsync(ct);
        return Result<BatchDto>.Ok(BatchMap.ToDto(batch));
    }
}

public class DeleteBatchCommandHandler : IRequestHandler<DeleteBatchCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteBatchCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteBatchCommand request, CancellationToken ct)
    {
        var batch = await _uow.Repository<FarmBatch>().GetByIdAsync(request.Id, ct);
        if (batch is null) throw new NotFoundException("Batch not found.");
        _uow.Repository<FarmBatch>().Remove(batch);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
