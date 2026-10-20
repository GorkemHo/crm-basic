using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.Modules.Sales.Domain;
using CRM.Modules.Sales.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Sales.Features.Stages;

internal sealed record StageDto(int Id, string Name, int Position, int Probability, int OpenDealCount);

// ---------------------------------------------------------------- List

/// <summary>All stages in board order.</summary>
internal sealed record GetStages : IQuery<IReadOnlyList<StageDto>>;

internal sealed class GetStagesHandler : IRequestHandler<GetStages, IReadOnlyList<StageDto>>
{
    private readonly SalesDbContext _db;

    public GetStagesHandler(SalesDbContext db) => _db = db;

    public async Task<IReadOnlyList<StageDto>> Handle(GetStages request, CancellationToken cancellationToken) =>
        await _db.Stages.AsNoTracking()
            .OrderBy(x => x.Position)
            .Select(s => new StageDto(s.Id, s.Name, s.Position, s.Probability,
                _db.Deals.Count(d => d.StageId == s.Id && d.Status == DealStatus.Open)))
            .ToListAsync(cancellationToken);
}

// ---------------------------------------------------------------- Create / update / reorder / delete

/// <summary>Adds a stage at the end of the pipeline.</summary>
internal sealed record CreateStage(string Name, int Probability) : ICommand<StageDto>;

internal sealed class CreateStageValidator : AbstractValidator<CreateStage>
{
    public CreateStageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Probability).InclusiveBetween(0, 100);
    }
}

internal sealed class CreateStageHandler : IRequestHandler<CreateStage, StageDto>
{
    private readonly SalesDbContext _db;

    public CreateStageHandler(SalesDbContext db) => _db = db;

    public async Task<StageDto> Handle(CreateStage request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await StageRules.EnsureNameIsFreeAsync(_db, name, excludeId: null, cancellationToken);

        var position = await _db.Stages.Select(x => (int?)x.Position).MaxAsync(cancellationToken) + 1 ?? 0;
        var stage = new PipelineStage { Name = name, Probability = request.Probability, Position = position };
        _db.Stages.Add(stage);
        await _db.SaveChangesAsync(cancellationToken);

        return new StageDto(stage.Id, stage.Name, stage.Position, stage.Probability, 0);
    }
}

/// <summary>Only non-null fields are changed.</summary>
internal sealed record UpdateStage(int Id, string? Name, int? Probability) : ICommand<StageDto>;

internal sealed class UpdateStageValidator : AbstractValidator<UpdateStage>
{
    public UpdateStageValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Probability!.Value).InclusiveBetween(0, 100).When(x => x.Probability.HasValue);
    }
}

internal sealed class UpdateStageHandler : IRequestHandler<UpdateStage, StageDto>
{
    private readonly SalesDbContext _db;

    public UpdateStageHandler(SalesDbContext db) => _db = db;

    public async Task<StageDto> Handle(UpdateStage request, CancellationToken cancellationToken)
    {
        var stage = await StageRules.FindAsync(_db, request.Id, cancellationToken);

        if(request.Name is not null)
        {
            var name = request.Name.Trim();
            await StageRules.EnsureNameIsFreeAsync(_db, name, stage.Id, cancellationToken);
            stage.Name = name;
        }

        if(request.Probability.HasValue) stage.Probability = request.Probability.Value;

        await _db.SaveChangesAsync(cancellationToken);

        var openDeals = await _db.Deals.CountAsync(d => d.StageId == stage.Id && d.Status == DealStatus.Open, cancellationToken);
        return new StageDto(stage.Id, stage.Name, stage.Position, stage.Probability, openDeals);
    }
}

/// <summary>Sets the board order; <see cref="StageIds"/> must list every stage exactly once.</summary>
internal sealed record ReorderStages(int[] StageIds) : ICommand<IReadOnlyList<StageDto>>;

internal sealed class ReorderStagesValidator : AbstractValidator<ReorderStages>
{
    public ReorderStagesValidator()
    {
        RuleFor(x => x.StageIds).NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Length).WithMessage("Each stage may appear only once.");
    }
}

internal sealed class ReorderStagesHandler : IRequestHandler<ReorderStages, IReadOnlyList<StageDto>>
{
    private readonly SalesDbContext _db;
    private readonly ISender _sender;

    public ReorderStagesHandler(SalesDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<IReadOnlyList<StageDto>> Handle(ReorderStages request, CancellationToken cancellationToken)
    {
        var stages = await _db.Stages.ToListAsync(cancellationToken);

        if(stages.Count != request.StageIds.Length || stages.Any(s => !request.StageIds.Contains(s.Id)))
        {
            throw new CRM.BuildingBlocks.Exceptions.ValidationException("stageIds", "The list must contain every stage exactly once.", "stage.reorder_incomplete");
        }

        foreach(var stage in stages)
        {
            stage.Position = Array.IndexOf(request.StageIds, stage.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new GetStages(), cancellationToken);
    }
}

internal sealed record DeleteStage(int Id) : ICommand;

internal sealed class DeleteStageHandler : IRequestHandler<DeleteStage, Unit>
{
    private readonly SalesDbContext _db;

    public DeleteStageHandler(SalesDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteStage request, CancellationToken cancellationToken)
    {
        var stage = await StageRules.FindAsync(_db, request.Id, cancellationToken);

        if(await _db.Deals.AnyAsync(x => x.StageId == stage.Id, cancellationToken))
        {
            throw new ConflictException("The stage has deals and cannot be deleted. Move them to another stage first.", "stage.has_deals");
        }

        if(await _db.Stages.CountAsync(cancellationToken) == 1)
        {
            throw new BusinessRuleException("The pipeline needs at least one stage.", "stage.last_stage");
        }

        _db.Stages.Remove(stage);

        // Close the gap so positions stay 0..n-1.
        var position = 0;
        foreach(var other in await _db.Stages.Where(x => x.Id != stage.Id).OrderBy(x => x.Position).ToListAsync(cancellationToken))
        {
            other.Position = position++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal static class StageRules
{
    public static async Task<PipelineStage> FindAsync(SalesDbContext db, int id, CancellationToken cancellationToken) =>
        await db.Stages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Stage", id);

    public static async Task EnsureNameIsFreeAsync(SalesDbContext db, string name, int? excludeId, CancellationToken cancellationToken)
    {
        if(await db.Stages.AnyAsync(x => x.Name.ToLower() == name.ToLower() && x.Id != excludeId, cancellationToken))
        {
            throw new ConflictException("A stage with the same name already exists.", "stage.name_taken");
        }
    }
}
