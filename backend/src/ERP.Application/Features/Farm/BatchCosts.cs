using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

// ---- Feed entries (consumption) ----

public record FeedEntryDto(Guid Id, Guid BatchId, Guid FeedId, DateOnly EntryDate, decimal Quantity, decimal Rate, decimal Amount);

[HasPermission(Permissions.Farm.FeedRecord)]
public record GetFeedEntriesQuery(Guid BatchId) : IRequest<IReadOnlyList<FeedEntryDto>>;

[HasPermission(Permissions.Farm.FeedRecord)]
public record AddFeedEntryCommand(Guid BatchId, Guid FeedId, DateOnly EntryDate, decimal Quantity, decimal Rate)
    : IRequest<Result<FeedEntryDto>>;

public class AddFeedEntryCommandValidator : AbstractValidator<AddFeedEntryCommand>
{
    public AddFeedEntryCommandValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public class GetFeedEntriesQueryHandler : IRequestHandler<GetFeedEntriesQuery, IReadOnlyList<FeedEntryDto>>
{
    private readonly IRepository<FeedEntry> _repo;
    public GetFeedEntriesQueryHandler(IRepository<FeedEntry> repo) => _repo = repo;

    public async Task<IReadOnlyList<FeedEntryDto>> Handle(GetFeedEntriesQuery request, CancellationToken ct)
        => await _repo.Query().Where(f => f.BatchId == request.BatchId).OrderByDescending(f => f.EntryDate)
            .Select(f => new FeedEntryDto(f.Id, f.BatchId, f.FeedId, f.EntryDate, f.Quantity, f.Rate, f.Amount))
            .ToListAsync(ct);
}

public class AddFeedEntryCommandHandler : IRequestHandler<AddFeedEntryCommand, Result<FeedEntryDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddFeedEntryCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<FeedEntryDto>> Handle(AddFeedEntryCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        if (await _uow.Repository<FarmBatch>().GetByIdAsync(request.BatchId, ct) is null)
            throw new NotFoundException("Batch not found.");
        if (await _uow.Repository<Feed>().GetByIdAsync(request.FeedId, ct) is null)
            return Result<FeedEntryDto>.Fail("resource.not_found", "Feed not found.");

        var entry = new FeedEntry
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            FeedId = request.FeedId,
            EntryDate = request.EntryDate,
            Quantity = request.Quantity,
            Rate = request.Rate
        };
        entry.ComputeAmount();
        await _uow.Repository<FeedEntry>().AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<FeedEntryDto>.Ok(new FeedEntryDto(entry.Id, entry.BatchId, entry.FeedId, entry.EntryDate, entry.Quantity, entry.Rate, entry.Amount));
    }
}

// ---- Medical records ----

public record MedicalRecordDto(Guid Id, Guid BatchId, string MedicineName, decimal Amount, decimal DoctorCharges, decimal Total, DateOnly RecordDate);

[HasPermission(Permissions.Farm.MedicalRecord)]
public record GetMedicalRecordsQuery(Guid BatchId) : IRequest<IReadOnlyList<MedicalRecordDto>>;

[HasPermission(Permissions.Farm.MedicalRecord)]
public record AddMedicalRecordCommand(Guid BatchId, string MedicineName, decimal Amount, decimal DoctorCharges, DateOnly RecordDate)
    : IRequest<Result<MedicalRecordDto>>;

public class AddMedicalRecordCommandValidator : AbstractValidator<AddMedicalRecordCommand>
{
    public AddMedicalRecordCommandValidator()
    {
        RuleFor(x => x.MedicineName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DoctorCharges).GreaterThanOrEqualTo(0);
    }
}

public class GetMedicalRecordsQueryHandler : IRequestHandler<GetMedicalRecordsQuery, IReadOnlyList<MedicalRecordDto>>
{
    private readonly IRepository<MedicalRecord> _repo;
    public GetMedicalRecordsQueryHandler(IRepository<MedicalRecord> repo) => _repo = repo;

    public async Task<IReadOnlyList<MedicalRecordDto>> Handle(GetMedicalRecordsQuery request, CancellationToken ct)
        => await _repo.Query().Where(m => m.BatchId == request.BatchId).OrderByDescending(m => m.RecordDate)
            .Select(m => new MedicalRecordDto(m.Id, m.BatchId, m.MedicineName, m.Amount, m.DoctorCharges, m.Amount + m.DoctorCharges, m.RecordDate))
            .ToListAsync(ct);
}

public class AddMedicalRecordCommandHandler : IRequestHandler<AddMedicalRecordCommand, Result<MedicalRecordDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddMedicalRecordCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<MedicalRecordDto>> Handle(AddMedicalRecordCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        if (await _uow.Repository<FarmBatch>().GetByIdAsync(request.BatchId, ct) is null)
            throw new NotFoundException("Batch not found.");

        var record = new MedicalRecord
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            MedicineName = request.MedicineName.Trim(),
            Amount = request.Amount,
            DoctorCharges = request.DoctorCharges,
            RecordDate = request.RecordDate
        };
        await _uow.Repository<MedicalRecord>().AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<MedicalRecordDto>.Ok(new MedicalRecordDto(record.Id, record.BatchId, record.MedicineName, record.Amount, record.DoctorCharges, record.Total, record.RecordDate));
    }
}

// ---- Batch expenses (labour / other) ----

public record BatchExpenseDto(Guid Id, Guid BatchId, string ExpenseKind, decimal Amount, DateOnly ExpenseDate, string? Description);

[HasPermission(Permissions.Farm.BatchManage)]
public record GetBatchExpensesQuery(Guid BatchId) : IRequest<IReadOnlyList<BatchExpenseDto>>;

[HasPermission(Permissions.Farm.BatchManage)]
public record AddBatchExpenseCommand(Guid BatchId, string ExpenseKind, decimal Amount, DateOnly ExpenseDate, string? Description)
    : IRequest<Result<BatchExpenseDto>>;

public class AddBatchExpenseCommandValidator : AbstractValidator<AddBatchExpenseCommand>
{
    private static readonly string[] Kinds = { "labour", "other" };
    public AddBatchExpenseCommandValidator()
    {
        RuleFor(x => x.ExpenseKind).Must(k => Kinds.Contains(k))
            .WithMessage("Batch expense kind must be 'labour' or 'other' (feed/medical have their own modules).");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}

public class GetBatchExpensesQueryHandler : IRequestHandler<GetBatchExpensesQuery, IReadOnlyList<BatchExpenseDto>>
{
    private readonly IRepository<BatchExpense> _repo;
    public GetBatchExpensesQueryHandler(IRepository<BatchExpense> repo) => _repo = repo;

    public async Task<IReadOnlyList<BatchExpenseDto>> Handle(GetBatchExpensesQuery request, CancellationToken ct)
        => await _repo.Query().Where(e => e.BatchId == request.BatchId).OrderByDescending(e => e.ExpenseDate)
            .Select(e => new BatchExpenseDto(e.Id, e.BatchId, e.ExpenseKind, e.Amount, e.ExpenseDate, e.Description))
            .ToListAsync(ct);
}

public class AddBatchExpenseCommandHandler : IRequestHandler<AddBatchExpenseCommand, Result<BatchExpenseDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public AddBatchExpenseCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<BatchExpenseDto>> Handle(AddBatchExpenseCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        if (await _uow.Repository<FarmBatch>().GetByIdAsync(request.BatchId, ct) is null)
            throw new NotFoundException("Batch not found.");

        var expense = new BatchExpense
        {
            BusinessId = businessId,
            BatchId = request.BatchId,
            ExpenseKind = request.ExpenseKind,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            Description = request.Description
        };
        await _uow.Repository<BatchExpense>().AddAsync(expense, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<BatchExpenseDto>.Ok(new BatchExpenseDto(expense.Id, expense.BatchId, expense.ExpenseKind, expense.Amount, expense.ExpenseDate, expense.Description));
    }
}
