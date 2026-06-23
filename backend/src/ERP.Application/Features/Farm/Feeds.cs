using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Domain.Farm;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Farm;

public record FeedDto(Guid Id, string FeedName, string? FeedType, string Uom, decimal Rate, bool IsActive);

[HasPermission(Permissions.Farm.FeedRecord)]
public record GetFeedsQuery : IRequest<IReadOnlyList<FeedDto>>;

[HasPermission(Permissions.Farm.FeedRecord)]
public record CreateFeedCommand(string FeedName, string? FeedType, string Uom, decimal Rate) : IRequest<Result<FeedDto>>;

[HasPermission(Permissions.Farm.FeedRecord)]
public record DeleteFeedCommand(Guid Id) : IRequest<Result>;

public class CreateFeedCommandValidator : AbstractValidator<CreateFeedCommand>
{
    public CreateFeedCommandValidator()
    {
        RuleFor(x => x.FeedName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public class GetFeedsQueryHandler : IRequestHandler<GetFeedsQuery, IReadOnlyList<FeedDto>>
{
    private readonly IRepository<Feed> _repo;
    public GetFeedsQueryHandler(IRepository<Feed> repo) => _repo = repo;

    public async Task<IReadOnlyList<FeedDto>> Handle(GetFeedsQuery request, CancellationToken ct)
        => await _repo.Query().OrderBy(f => f.FeedName)
            .Select(f => new FeedDto(f.Id, f.FeedName, f.FeedType, f.Uom, f.Rate, f.IsActive)).ToListAsync(ct);
}

public class CreateFeedCommandHandler : IRequestHandler<CreateFeedCommand, Result<FeedDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;
    public CreateFeedCommandHandler(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<FeedDto>> Handle(CreateFeedCommand request, CancellationToken ct)
    {
        var businessId = AccessGuard.RequireBusiness(_currentUser);
        var feed = new Feed
        {
            BusinessId = businessId,
            FeedName = request.FeedName.Trim(),
            FeedType = request.FeedType,
            Uom = string.IsNullOrWhiteSpace(request.Uom) ? "kg" : request.Uom,
            Rate = request.Rate
        };
        await _uow.Repository<Feed>().AddAsync(feed, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<FeedDto>.Ok(new FeedDto(feed.Id, feed.FeedName, feed.FeedType, feed.Uom, feed.Rate, feed.IsActive));
    }
}

public class DeleteFeedCommandHandler : IRequestHandler<DeleteFeedCommand, Result>
{
    private readonly IUnitOfWork _uow;
    public DeleteFeedCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> Handle(DeleteFeedCommand request, CancellationToken ct)
    {
        var feed = await _uow.Repository<Feed>().GetByIdAsync(request.Id, ct);
        if (feed is null) throw new NotFoundException("Feed not found.");
        _uow.Repository<Feed>().Remove(feed);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
