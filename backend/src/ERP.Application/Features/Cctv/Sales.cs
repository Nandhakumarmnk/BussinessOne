using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Application.Features.Customers;
using ERP.Domain.Cctv;
using ERP.Domain.Customers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record SaleLineInput(Guid ItemId, decimal Quantity, decimal Rate, decimal TaxPercentage);
public record SaleLineDto(Guid Id, Guid ItemId, decimal Quantity, decimal Rate, decimal TaxPercentage, decimal LineTotal);
public record SaleDto(
    Guid Id, string InvoiceNumber, Guid? CustomerId, string? CustomerName, DateOnly SaleDate,
    decimal InstallationCharges, decimal LabourCharges, decimal SubTotal, decimal TaxAmount, decimal TotalAmount,
    decimal PaidAmount, decimal Balance, string Status, IReadOnlyList<SaleLineDto> Lines);

[HasPermission(Permissions.Cctv.SaleCreate)]
public record GetSalesQuery(DateOnly? From, DateOnly? To) : IRequest<IReadOnlyList<SaleDto>>;

[HasPermission(Permissions.Cctv.SaleCreate)]
public record GetSaleQuery(Guid Id) : IRequest<SaleDto>;

[HasPermission(Permissions.Cctv.SaleCreate)]
public record CreateSaleCommand(
    string InvoiceNumber, Guid? CustomerId, DateOnly SaleDate, decimal InstallationCharges, decimal LabourCharges,
    decimal PaidAmount, string Mode, IReadOnlyList<SaleLineInput> Lines) : IRequest<Result<SaleDto>>;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleFor(x => x.InstallationCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LabourCharges).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidAmount).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
            l.RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
        });
    }
}

internal static class SaleMap
{
    public static SaleDto ToDto(Sale s, string? customerName) => new(
        s.Id, s.InvoiceNumber, s.CustomerId, customerName, s.SaleDate, s.InstallationCharges, s.LabourCharges,
        s.SubTotal, s.TaxAmount, s.TotalAmount, s.PaidAmount, s.Balance, s.Status,
        s.Lines.Select(l => new SaleLineDto(l.Id, l.ItemId, l.Quantity, l.Rate, l.TaxPercentage, l.LineTotal)).ToList());
}

public class GetSalesQueryHandler : IRequestHandler<GetSalesQuery, IReadOnlyList<SaleDto>>
{
    private readonly IRepository<Sale> _sales;
    private readonly IRepository<Customer> _customers;
    public GetSalesQueryHandler(IRepository<Sale> sales, IRepository<Customer> customers)
    {
        _sales = sales;
        _customers = customers;
    }

    public async Task<IReadOnlyList<SaleDto>> Handle(GetSalesQuery request, CancellationToken ct)
    {
        var customers = _customers;
        var q = _sales.Query();
        if (request.From is { } from) q = q.Where(s => s.SaleDate >= from);
        if (request.To is { } to) q = q.Where(s => s.SaleDate <= to);

        return await q.OrderByDescending(s => s.SaleDate)
            .Select(s => new SaleDto(
                s.Id, s.InvoiceNumber, s.CustomerId,
                customers.Query().Where(c => c.Id == s.CustomerId).Select(c => c.Name).FirstOrDefault(),
                s.SaleDate, s.InstallationCharges, s.LabourCharges, s.SubTotal, s.TaxAmount, s.TotalAmount,
                s.PaidAmount, s.TotalAmount - s.PaidAmount, s.Status, new List<SaleLineDto>()))
            .ToListAsync(ct);
    }
}

public class GetSaleQueryHandler : IRequestHandler<GetSaleQuery, SaleDto>
{
    private readonly IRepository<Sale> _sales;
    private readonly IRepository<SaleLine> _lines;
    private readonly IRepository<Customer> _customers;
    public GetSaleQueryHandler(IRepository<Sale> sales, IRepository<SaleLine> lines, IRepository<Customer> customers)
    {
        _sales = sales;
        _lines = lines;
        _customers = customers;
    }

    public async Task<SaleDto> Handle(GetSaleQuery request, CancellationToken ct)
    {
        var sale = await _sales.Query().FirstOrDefaultAsync(s => s.Id == request.Id, ct)
                   ?? throw new NotFoundException("Sale not found.");
        var lines = await _lines.Query().Where(l => l.SaleId == sale.Id).ToListAsync(ct);
        var customerName = sale.CustomerId is null ? null
            : await _customers.Query().Where(c => c.Id == sale.CustomerId).Select(c => c.Name).FirstOrDefaultAsync(ct);

        return new SaleDto(sale.Id, sale.InvoiceNumber, sale.CustomerId, customerName, sale.SaleDate,
            sale.InstallationCharges, sale.LabourCharges, sale.SubTotal, sale.TaxAmount, sale.TotalAmount,
            sale.PaidAmount, sale.TotalAmount - sale.PaidAmount, sale.Status,
            lines.Select(l => new SaleLineDto(l.Id, l.ItemId, l.Quantity, l.Rate, l.TaxPercentage, l.LineTotal)).ToList());
    }
}

public class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<SaleDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly CustomerLedgerService _ledger;

    public CreateSaleCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, CustomerLedgerService ledger)
    {
        _currentUser = currentUser;
        _uow = uow;
        _ledger = ledger;
    }

    public async Task<Result<SaleDto>> Handle(CreateSaleCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var invoice = request.InvoiceNumber.Trim();
        if (await _uow.Repository<Sale>().Query().AnyAsync(s => s.InvoiceNumber == invoice, ct))
            return Result<SaleDto>.Fail("resource.conflict", "A sale with that invoice number already exists.");

        Customer? customer = null;
        if (request.CustomerId is { } cid)
        {
            customer = await _uow.Repository<Customer>().GetByIdAsync(cid, ct);
            if (customer is null) return Result<SaleDto>.Fail("resource.not_found", "Customer not found.");
        }

        var sale = new Sale
        {
            BusinessId = businessId,
            InvoiceNumber = invoice,
            CustomerId = request.CustomerId,
            SaleDate = request.SaleDate,
            InstallationCharges = request.InstallationCharges,
            LabourCharges = request.LabourCharges,
            PaidAmount = request.PaidAmount
        };

        foreach (var input in request.Lines)
        {
            var item = await _uow.Repository<Item>().GetByIdAsync(input.ItemId, ct);
            if (item is null) return Result<SaleDto>.Fail("resource.not_found", $"Item {input.ItemId} not found.");

            var line = new SaleLine
            {
                SaleId = sale.Id,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                Rate = input.Rate,
                TaxPercentage = input.TaxPercentage
            };
            line.ComputeTotal();
            sale.Lines.Add(line);

            item.StockQuantity -= input.Quantity;   // stock-out
            _uow.Repository<Item>().Update(item);
        }
        sale.RecalculateTotals();

        if (sale.PaidAmount > sale.TotalAmount)
            return Result<SaleDto>.Fail("validation.failed", "Paid amount cannot exceed the sale total.");

        await _uow.Repository<Sale>().AddAsync(sale, ct);

        // Account for the receivable + any immediate payment on the customer ledger.
        if (request.CustomerId is { } customerId)
        {
            await _ledger.AppendAsync(businessId, customerId, sale.SaleDate, "sale", sale.Id, sale.TotalAmount, 0, ct);
            if (sale.PaidAmount > 0)
            {
                await _ledger.AppendAsync(businessId, customerId, sale.SaleDate, "collection", sale.Id, 0, sale.PaidAmount, ct);
                await _uow.Repository<Collection>().AddAsync(new Collection
                {
                    BusinessId = businessId,
                    CustomerId = customerId,
                    CollectionDate = sale.SaleDate,
                    Amount = sale.PaidAmount,
                    Mode = string.IsNullOrWhiteSpace(request.Mode) ? "cash" : request.Mode,
                    Reference = $"Invoice {sale.InvoiceNumber}"
                }, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Result<SaleDto>.Ok(SaleMap.ToDto(sale, customer?.Name));
    }
}
