using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Cctv;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Cctv;

public record SupplierDto(Guid Id, string Name, string? Mobile, string? GstNumber, string? Address);

[HasPermission(Permissions.Cctv.ItemManage)]
public record GetSuppliersQuery : IRequest<IReadOnlyList<SupplierDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record CreateSupplierCommand(string Name, string? Mobile, string? GstNumber, string? Address)
    : IRequest<Result<SupplierDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record UpdateSupplierCommand(Guid Id, string Name, string? Mobile, string? GstNumber, string? Address)
    : IRequest<Result<SupplierDto>>;

[HasPermission(Permissions.Cctv.ItemManage)]
public record DeleteSupplierCommand(Guid Id) : IRequest<Result>;

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
}

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    private readonly IRepository<Supplier> _repo;
    public GetSuppliersQueryHandler(IRepository<Supplier> repo) => _repo = repo;

    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Mobile, s.GstNumber, s.Address)).ToListAsync(ct);
}

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateSupplierCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<SupplierDto>> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var supplier = new Supplier
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Mobile = request.Mobile,
            GstNumber = request.GstNumber,
            Address = request.Address
        };
        await _uow.Repository<Supplier>().AddAsync(supplier, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<SupplierDto>.Ok(new SupplierDto(supplier.Id, supplier.Name, supplier.Mobile, supplier.GstNumber, supplier.Address));
    }
}

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Result<SupplierDto>>
{
    private readonly IUnitOfWork _uow;
    public UpdateSupplierCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<SupplierDto>> Handle(UpdateSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(request.Id, ct);
        if (supplier is null) throw new NotFoundException("Supplier not found.");

        supplier.Name = request.Name.Trim();
        supplier.Mobile = request.Mobile;
        supplier.GstNumber = request.GstNumber;
        supplier.Address = request.Address;
        _uow.Repository<Supplier>().Update(supplier);
        await _uow.SaveChangesAsync(ct);
        return Result<SupplierDto>.Ok(new SupplierDto(supplier.Id, supplier.Name, supplier.Mobile, supplier.GstNumber, supplier.Address));
    }
}

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteSupplierCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(request.Id, ct);
        if (supplier is null) throw new NotFoundException("Supplier not found.");
        _uow.Repository<Supplier>().Remove(supplier);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
