using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record ItemDto(
    Guid Id, string ItemCode, string ItemName, string Uom, string? HsnCode,
    decimal Rate, decimal TaxPercentage, decimal StockQuantity, decimal ReorderLevel, bool IsActive);

[HasPermission(Permissions.Cctv.ItemManage)]
public record GetItemsQuery : IRequest<IReadOnlyList<ItemDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record CreateItemCommand(
    string ItemCode, string ItemName, string Uom, string? HsnCode,
    decimal Rate, decimal TaxPercentage, decimal ReorderLevel) : IRequest<Result<ItemDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record UpdateItemCommand(
    Guid Id, string ItemCode, string ItemName, string Uom, string? HsnCode,
    decimal Rate, decimal TaxPercentage, decimal ReorderLevel, bool IsActive) : IRequest<Result<ItemDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record DeleteItemCommand(Guid Id) : IRequest<Result>;

public class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.ItemCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
    }
}

public class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.ItemCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
    }
}

internal static class ItemMap
{
    public static ItemDto ToDto(Item i) => new(
        i.Id, i.ItemCode, i.ItemName, i.Uom, i.HsnCode, i.Rate, i.TaxPercentage, i.StockQuantity, i.ReorderLevel, i.IsActive);
}

public class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, IReadOnlyList<ItemDto>>
{
    private readonly IRepository<Item> _repo;
    public GetItemsQueryHandler(IRepository<Item> repo) => _repo = repo;

    public async Task<IReadOnlyList<ItemDto>> Handle(GetItemsQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(i => i.ItemName)
            .Select(i => new ItemDto(i.Id, i.ItemCode, i.ItemName, i.Uom, i.HsnCode, i.Rate, i.TaxPercentage,
                i.StockQuantity, i.ReorderLevel, i.IsActive))
            .ToListAsync(ct);
}

public class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, Result<ItemDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateItemCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<ItemDto>> Handle(CreateItemCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var code = request.ItemCode.Trim();
        if (await _uow.Repository<Item>().Query().AnyAsync(i => i.ItemCode == code, ct))
            return Result<ItemDto>.Fail("resource.conflict", "An item with that code already exists.");

        var item = new Item
        {
            BusinessId = businessId,
            ItemCode = code,
            ItemName = request.ItemName.Trim(),
            Uom = string.IsNullOrWhiteSpace(request.Uom) ? "nos" : request.Uom,
            HsnCode = request.HsnCode,
            Rate = request.Rate,
            TaxPercentage = request.TaxPercentage,
            ReorderLevel = request.ReorderLevel
        };
        await _uow.Repository<Item>().AddAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ItemDto>.Ok(ItemMap.ToDto(item));
    }
}

public class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand, Result<ItemDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateItemCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<ItemDto>> Handle(UpdateItemCommand request, CancellationToken ct)
    {
        var item = await _uow.Repository<Item>().GetByIdAsync(request.Id, ct);
        if (item is null) throw new NotFoundException("Item not found.");

        item.ItemCode = request.ItemCode.Trim();
        item.ItemName = request.ItemName.Trim();
        item.Uom = request.Uom;
        item.HsnCode = request.HsnCode;
        item.Rate = request.Rate;
        item.TaxPercentage = request.TaxPercentage;
        item.ReorderLevel = request.ReorderLevel;
        item.IsActive = request.IsActive;
        _uow.Repository<Item>().Update(item);
        await _uow.SaveChangesAsync(ct);
        return Result<ItemDto>.Ok(ItemMap.ToDto(item));
    }
}

public class DeleteItemCommandHandler : IRequestHandler<DeleteItemCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteItemCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteItemCommand request, CancellationToken ct)
    {
        var item = await _uow.Repository<Item>().GetByIdAsync(request.Id, ct);
        if (item is null) throw new NotFoundException("Item not found.");
        _uow.Repository<Item>().Remove(item);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
