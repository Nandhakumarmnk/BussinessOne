using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record PoLineInput(Guid ItemId, decimal Quantity, decimal Rate, decimal TaxPercentage);
public record PoLineDto(Guid Id, Guid ItemId, decimal Quantity, decimal Rate, decimal TaxPercentage, decimal LineTotal);
public record PurchaseOrderDto(
    Guid Id, string PoNumber, Guid SupplierId, string? SupplierName, DateOnly PoDate,
    decimal TotalAmount, string Status, IReadOnlyList<PoLineDto> Lines);

[HasPermission(Permissions.Cctv.PoCreate)]
public record GetPurchaseOrdersQuery(string? Status) : IRequest<IReadOnlyList<PurchaseOrderDto>>;

[HasPermission(Permissions.Cctv.PoCreate)]
public record GetPurchaseOrderQuery(Guid Id) : IRequest<PurchaseOrderDto>;

[HasPermission(Permissions.Cctv.PoCreate)]
public record CreatePurchaseOrderCommand(
    string PoNumber, Guid SupplierId, DateOnly PoDate, string? Note, IReadOnlyList<PoLineInput> Lines)
    : IRequest<Result<PurchaseOrderDto>>;

[HasPermission(Permissions.Cctv.PoCreate)]
public record SubmitPurchaseOrderCommand(Guid Id) : IRequest<Result>;

[HasPermission(Permissions.Cctv.PoApprove)]
public record ApprovePurchaseOrderCommand(Guid Id) : IRequest<Result>;

[HasPermission(Permissions.Cctv.PoCreate)]
public record ReceivePurchaseOrderCommand(Guid Id) : IRequest<Result>;

[HasPermission(Permissions.Cctv.PoCreate)]
public record CancelPurchaseOrderCommand(Guid Id) : IRequest<Result>;

public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.PoNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
        });
    }
}

internal static class PoMap
{
    public static PurchaseOrderDto ToDto(PurchaseOrder po, string? supplierName) => new(
        po.Id, po.PoNumber, po.SupplierId, supplierName, po.PoDate, po.TotalAmount, po.Status,
        po.Lines.Select(l => new PoLineDto(l.Id, l.ItemId, l.Quantity, l.Rate, l.TaxPercentage, l.LineTotal)).ToList());
}

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>
{
    private readonly IRepository<PurchaseOrder> _orders;
    private readonly IRepository<Supplier> _suppliers;
    public GetPurchaseOrdersQueryHandler(IRepository<PurchaseOrder> orders, IRepository<Supplier> suppliers)
    {
        _orders = orders;
        _suppliers = suppliers;
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var suppliers = _suppliers;
        var q = _orders.Query();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(p => p.Status == request.Status);

        return await q.OrderByDescending(p => p.PoDate)
            .Select(p => new PurchaseOrderDto(
                p.Id, p.PoNumber, p.SupplierId,
                suppliers.Query().Where(s => s.Id == p.SupplierId).Select(s => s.Name).FirstOrDefault(),
                p.PoDate, p.TotalAmount, p.Status, new List<PoLineDto>()))
            .ToListAsync(ct);
    }
}

public class GetPurchaseOrderQueryHandler : IRequestHandler<GetPurchaseOrderQuery, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _orders;
    private readonly IRepository<PurchaseOrderLine> _lines;
    private readonly IRepository<Supplier> _suppliers;
    public GetPurchaseOrderQueryHandler(
        IRepository<PurchaseOrder> orders, IRepository<PurchaseOrderLine> lines, IRepository<Supplier> suppliers)
    {
        _orders = orders;
        _lines = lines;
        _suppliers = suppliers;
    }

    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderQuery request, CancellationToken ct)
    {
        var po = await _orders.Query().FirstOrDefaultAsync(p => p.Id == request.Id, ct)
                 ?? throw new NotFoundException("Purchase order not found.");
        var lines = await _lines.Query().Where(l => l.PurchaseOrderId == po.Id).ToListAsync(ct);
        var supplierName = await _suppliers.Query().Where(s => s.Id == po.SupplierId).Select(s => s.Name).FirstOrDefaultAsync(ct);

        return new PurchaseOrderDto(po.Id, po.PoNumber, po.SupplierId, supplierName, po.PoDate, po.TotalAmount, po.Status,
            lines.Select(l => new PoLineDto(l.Id, l.ItemId, l.Quantity, l.Rate, l.TaxPercentage, l.LineTotal)).ToList());
    }
}

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreatePurchaseOrderCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var number = request.PoNumber.Trim();
        if (await _uow.Repository<PurchaseOrder>().Query().AnyAsync(p => p.PoNumber == number, ct))
            return Result<PurchaseOrderDto>.Fail("resource.conflict", "A PO with that number already exists.");

        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(request.SupplierId, ct);
        if (supplier is null) return Result<PurchaseOrderDto>.Fail("resource.not_found", "Supplier not found.");

        var po = new PurchaseOrder
        {
            BusinessId = businessId,
            PoNumber = number,
            SupplierId = request.SupplierId,
            PoDate = request.PoDate,
            Note = request.Note
        };

        foreach (var input in request.Lines)
        {
            if (await _uow.Repository<Item>().GetByIdAsync(input.ItemId, ct) is null)
                return Result<PurchaseOrderDto>.Fail("resource.not_found", $"Item {input.ItemId} not found.");

            var line = new PurchaseOrderLine
            {
                PurchaseOrderId = po.Id,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                Rate = input.Rate,
                TaxPercentage = input.TaxPercentage
            };
            line.ComputeTotal();
            po.Lines.Add(line);
        }
        po.RecalculateTotal();

        await _uow.Repository<PurchaseOrder>().AddAsync(po, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PurchaseOrderDto>.Ok(PoMap.ToDto(po, supplier.Name));
    }
}

// ---- State transitions ----

public class SubmitPurchaseOrderCommandHandler : IRequestHandler<SubmitPurchaseOrderCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public SubmitPurchaseOrderCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(SubmitPurchaseOrderCommand request, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(request.Id, ct);
        if (po is null) throw new NotFoundException("Purchase order not found.");
        if (!po.Submit()) return Result.Fail("resource.conflict", "Only a draft PO can be submitted.");
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IDateTime _clock;
    public ApprovePurchaseOrderCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, IDateTime clock)
    {
        _currentUser = currentUser;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result> Handle(ApprovePurchaseOrderCommand request, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(request.Id, ct);
        if (po is null) throw new NotFoundException("Purchase order not found.");
        if (!po.Approve(_currentUser.UserId ?? Guid.Empty, _clock.UtcNow))
            return Result.Fail("resource.conflict", "Only a pending PO can be approved.");
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class ReceivePurchaseOrderCommandHandler : IRequestHandler<ReceivePurchaseOrderCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public ReceivePurchaseOrderCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(ReceivePurchaseOrderCommand request, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(request.Id, ct);
        if (po is null) throw new NotFoundException("Purchase order not found.");
        if (po.Status != PoStatus.Approved)
            return Result.Fail("resource.conflict", "Only an approved PO can be received.");

        var lines = await _uow.Repository<PurchaseOrderLine>().Query().Where(l => l.PurchaseOrderId == po.Id).ToListAsync(ct);
        foreach (var line in lines)
        {
            var item = await _uow.Repository<Item>().GetByIdAsync(line.ItemId, ct);
            if (item is null) continue;
            item.StockQuantity += line.Quantity;   // stock-in
            _uow.Repository<Item>().Update(item);
        }

        po.Receive();
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}

public class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public CancelPurchaseOrderCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(CancelPurchaseOrderCommand request, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(request.Id, ct);
        if (po is null) throw new NotFoundException("Purchase order not found.");
        if (!po.Cancel()) return Result.Fail("resource.conflict", "A received or cancelled PO cannot be cancelled.");
        _uow.Repository<PurchaseOrder>().Update(po);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
