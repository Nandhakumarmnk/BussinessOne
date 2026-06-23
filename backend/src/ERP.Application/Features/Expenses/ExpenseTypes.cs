using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Expenses;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Expenses;

public record ExpenseTypeDto(Guid Id, string Name, bool IsActive);

[HasPermission(Permissions.Expense.Manage)]
public record GetExpenseTypesQuery : IRequest<IReadOnlyList<ExpenseTypeDto>>;

[HasPermission(Permissions.Expense.Manage)]
public record CreateExpenseTypeCommand(string Name) : IRequest<Result<ExpenseTypeDto>>;

[HasPermission(Permissions.Expense.Manage)]
public record DeleteExpenseTypeCommand(Guid Id) : IRequest<Result>;

public class CreateExpenseTypeCommandValidator : AbstractValidator<CreateExpenseTypeCommand>
{
    public CreateExpenseTypeCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
}

public class GetExpenseTypesQueryHandler : IRequestHandler<GetExpenseTypesQuery, IReadOnlyList<ExpenseTypeDto>>
{
    private readonly IRepository<ExpenseType> _repo;
    public GetExpenseTypesQueryHandler(IRepository<ExpenseType> repo) => _repo = repo;

    public async Task<IReadOnlyList<ExpenseTypeDto>> Handle(GetExpenseTypesQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(t => t.Name)
            .Select(t => new ExpenseTypeDto(t.Id, t.Name, t.IsActive)).ToListAsync(ct);
}

public class CreateExpenseTypeCommandHandler : IRequestHandler<CreateExpenseTypeCommand, Result<ExpenseTypeDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateExpenseTypeCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<ExpenseTypeDto>> Handle(CreateExpenseTypeCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var name = request.Name.Trim();

        if (await _uow.Repository<ExpenseType>().Query().AnyAsync(t => t.Name == name, ct))
            return Result<ExpenseTypeDto>.Fail("resource.conflict", "An expense type with that name already exists.");

        var type = new ExpenseType { BusinessId = businessId, Name = name };
        await _uow.Repository<ExpenseType>().AddAsync(type, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ExpenseTypeDto>.Ok(new ExpenseTypeDto(type.Id, type.Name, type.IsActive));
    }
}

public class DeleteExpenseTypeCommandHandler : IRequestHandler<DeleteExpenseTypeCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteExpenseTypeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteExpenseTypeCommand request, CancellationToken ct)
    {
        var type = await _uow.Repository<ExpenseType>().GetByIdAsync(request.Id, ct);
        if (type is null) throw new NotFoundException("Expense type not found.");
        _uow.Repository<ExpenseType>().Remove(type);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
