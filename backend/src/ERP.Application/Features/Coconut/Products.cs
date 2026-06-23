using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Coconut;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Coconut;

public record ProductDto(Guid Id, string Name, string? Category, string Uom, bool IsActive);

[HasPermission(Permissions.Coconut.BatchManage)]
public record GetProductsQuery : IRequest<IReadOnlyList<ProductDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record CreateProductCommand(string Name, string? Category, string Uom) : IRequest<Result<ProductDto>>;

[HasPermission(Permissions.Coconut.BatchManage)]
public record DeleteProductCommand(Guid Id) : IRequest<Result>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
}

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    private readonly IRepository<Product> _repo;
    public GetProductsQueryHandler(IRepository<Product> repo) => _repo = repo;

    public async Task<IReadOnlyList<ProductDto>> Handle(GetProductsQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Category, p.Uom, p.IsActive)).ToListAsync(ct);
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateProductCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var name = request.Name.Trim();
        if (await _uow.Repository<Product>().Query().AnyAsync(p => p.Name == name, ct))
            return Result<ProductDto>.Fail("resource.conflict", "A product with that name already exists.");

        var product = new Product
        {
            BusinessId = businessId,
            Name = name,
            Category = request.Category,
            Uom = string.IsNullOrWhiteSpace(request.Uom) ? "kg" : request.Uom
        };
        await _uow.Repository<Product>().AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ProductDto>.Ok(new ProductDto(product.Id, product.Name, product.Category, product.Uom, product.IsActive));
    }
}

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteProductCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().GetByIdAsync(request.Id, ct);
        if (product is null) throw new NotFoundException("Product not found.");
        _uow.Repository<Product>().Remove(product);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
