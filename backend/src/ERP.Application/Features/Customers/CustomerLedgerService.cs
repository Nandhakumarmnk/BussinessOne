using ERP.Application.Common.Interfaces;
using ERP.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Customers;

/// <summary>
/// Appends a customer-ledger entry and maintains the running balance. Shared by collections now,
/// and by loads/sales in later phases (debit when credit is extended).
/// </summary>
public class CustomerLedgerService
{
    private readonly IUnitOfWork _uow;
    public CustomerLedgerService(IUnitOfWork uow) => _uow = uow;

    public async Task<CustomerLedgerEntry> AppendAsync(
        Guid businessId, Guid customerId, DateOnly entryDate, string refType, Guid? refId,
        decimal debit, decimal credit, CancellationToken ct)
    {
        var priorBalance = await _uow.Repository<CustomerLedgerEntry>().Query()
            .Where(l => l.CustomerId == customerId)
            .SumAsync(l => l.Debit - l.Credit, ct);

        var entry = new CustomerLedgerEntry
        {
            BusinessId = businessId,
            CustomerId = customerId,
            EntryDate = entryDate,
            RefType = refType,
            RefId = refId,
            Debit = debit,
            Credit = credit,
            RunningBalance = priorBalance + debit - credit
        };
        await _uow.Repository<CustomerLedgerEntry>().AddAsync(entry, ct);
        return entry;
    }
}
