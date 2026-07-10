using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Expenses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Expenses;

public record ExpenseDto(
    Guid Id, Guid? ExpenseTypeId, string? ExpenseTypeName, DateOnly ExpenseDate,
    decimal Amount, string? Description, string? AttachmentKey);

[HasPermission(Permissions.Expense.Manage)]
public record GetExpensesQuery(DateOnly? From, DateOnly? To, Guid? TypeId) : IRequest<IReadOnlyList<ExpenseDto>>;

[HasPermission(Permissions.Expense.Manage)]
public record CreateExpenseCommand(
    Guid? ExpenseTypeId, DateOnly ExpenseDate, decimal Amount, string? Description, string? AttachmentKey)
    : IRequest<Result<ExpenseDto>>;

[HasPermission(Permissions.Expense.Manage)]
public record UpdateExpenseCommand(
    Guid Id, Guid? ExpenseTypeId, DateOnly ExpenseDate, decimal Amount, string? Description, string? AttachmentKey)
    : IRequest<Result<ExpenseDto>>;

[HasPermission(Permissions.Expense.Manage)]
public record DeleteExpenseCommand(Guid Id) : IRequest<Result>;

public record AttachmentUrlDto(string Url);

[HasPermission(Permissions.Expense.Manage)]
public record GetExpenseAttachmentUrlQuery(Guid ExpenseId) : IRequest<Result<AttachmentUrlDto>>;

public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator() => RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
}

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator() => RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
}

public class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, IReadOnlyList<ExpenseDto>>
{
    private readonly IRepository<Expense> _expenses;
    private readonly IRepository<ExpenseType> _types;

    public GetExpensesQueryHandler(IRepository<Expense> expenses, IRepository<ExpenseType> types)
    {
        _expenses = expenses;
        _types = types;
    }

    public async Task<IReadOnlyList<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken ct)
    {
        var types = _types;
        var query = _expenses.Query();
        if (request.From is { } from) query = query.Where(e => e.ExpenseDate >= from);
        if (request.To is { } to) query = query.Where(e => e.ExpenseDate <= to);
        if (request.TypeId is { } typeId) query = query.Where(e => e.ExpenseTypeId == typeId);

        return await query.OrderByDescending(e => e.ExpenseDate)
            .Select(e => new ExpenseDto(
                e.Id, e.ExpenseTypeId,
                types.Query().Where(t => t.Id == e.ExpenseTypeId).Select(t => t.Name).FirstOrDefault(),
                e.ExpenseDate, e.Amount, e.Description, e.AttachmentKey))
            .ToListAsync(ct);
    }
}

public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, Result<ExpenseDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IJournalService _journal;
    public CreateExpenseCommandHandler(ICurrentUser currentUser, IUnitOfWork uow, IJournalService journal)
    {
        _currentUser = currentUser;
        _uow = uow;
        _journal = journal;
    }

    public async Task<Result<ExpenseDto>> Handle(CreateExpenseCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var expense = new Expense
        {
            BusinessId = businessId,
            ExpenseTypeId = request.ExpenseTypeId,
            ExpenseDate = request.ExpenseDate,
            Amount = request.Amount,
            Description = request.Description,
            AttachmentKey = request.AttachmentKey
        };
        await _uow.Repository<Expense>().AddAsync(expense, ct);

        // Double-entry: Dr Expenses, Cr Cash (posted atomically with the expense).
        if (expense.Amount > 0)
        {
            await _journal.PostAsync(businessId, expense.ExpenseDate, "expense", expense.Id,
                request.Description ?? "Expense", new[]
                {
                    new JournalLine(AccountCodes.Expenses, expense.Amount, 0),
                    new JournalLine(AccountCodes.Cash, 0, expense.Amount)
                }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<ExpenseDto>.Ok(new ExpenseDto(
            expense.Id, expense.ExpenseTypeId, null, expense.ExpenseDate, expense.Amount,
            expense.Description, expense.AttachmentKey));
    }
}

public class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Result<ExpenseDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateExpenseCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<ExpenseDto>> Handle(UpdateExpenseCommand request, CancellationToken ct)
    {
        var expense = await _uow.Repository<Expense>().GetByIdAsync(request.Id, ct);
        if (expense is null) throw new NotFoundException("Expense not found.");

        expense.ExpenseTypeId = request.ExpenseTypeId;
        expense.ExpenseDate = request.ExpenseDate;
        expense.Amount = request.Amount;
        expense.Description = request.Description;
        expense.AttachmentKey = request.AttachmentKey;
        _uow.Repository<Expense>().Update(expense);
        await _uow.SaveChangesAsync(ct);

        return Result<ExpenseDto>.Ok(new ExpenseDto(
            expense.Id, expense.ExpenseTypeId, null, expense.ExpenseDate, expense.Amount,
            expense.Description, expense.AttachmentKey));
    }
}

public class GetExpenseAttachmentUrlQueryHandler
    : IRequestHandler<GetExpenseAttachmentUrlQuery, Result<AttachmentUrlDto>>
{
    private readonly IRepository<Expense> _expenses;
    private readonly IFileStorage _storage;

    public GetExpenseAttachmentUrlQueryHandler(IRepository<Expense> expenses, IFileStorage storage)
    {
        _expenses = expenses;
        _storage = storage;
    }

    public async Task<Result<AttachmentUrlDto>> Handle(GetExpenseAttachmentUrlQuery request, CancellationToken ct)
    {
        // Query() honours the business global query filter, so a caller can only resolve attachments
        // for expenses in their active business. (GetByIdAsync uses FindAsync, which bypasses the
        // filter — deliberately not used here, else keys could be read across businesses.)
        var key = await _expenses.Query()
            .Where(e => e.Id == request.ExpenseId)
            .Select(e => e.AttachmentKey)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(key))
            return Result<AttachmentUrlDto>.Fail("resource.not_found", "Attachment not found.");

        var url = await _storage.GetDownloadUrlAsync(key, ct);
        return Result<AttachmentUrlDto>.Ok(new AttachmentUrlDto(url));
    }
}

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteExpenseCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteExpenseCommand request, CancellationToken ct)
    {
        var expense = await _uow.Repository<Expense>().GetByIdAsync(request.Id, ct);
        if (expense is null) throw new NotFoundException("Expense not found.");
        _uow.Repository<Expense>().Remove(expense);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
