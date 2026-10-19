using System.Linq.Expressions;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Pagination;
using CRM.Modules.Crm.Domain;
using CRM.Modules.Crm.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Crm.Features.Companies;

internal sealed record CompanyDto(
    int Id,
    string CompanyName,
    string? TaxNumber,
    bool IsActive,
    int CustomerCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static readonly Expression<Func<Company, CompanyDto>> Projection = c => new CompanyDto(
        c.Id, c.CompanyName, c.TaxNumber, c.IsActive, c.Customers.Count, c.CreatedAt, c.UpdatedAt);

    public static readonly Func<Company, CompanyDto> Compiled = Projection.Compile();
}

// ---------------------------------------------------------------- List / get

internal sealed class GetCompanies : PaginationRequest, IQuery<PagedResponse<CompanyDto>>
{
    /// <summary>Matches company name or tax number (case-insensitive).</summary>
    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}

internal sealed class GetCompaniesHandler : IRequestHandler<GetCompanies, PagedResponse<CompanyDto>>
{
    private readonly CrmDbContext _db;

    public GetCompaniesHandler(CrmDbContext db) => _db = db;

    public Task<PagedResponse<CompanyDto>> Handle(GetCompanies request, CancellationToken cancellationToken)
    {
        var query = _db.Companies.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.CompanyName, pattern) || EF.Functions.ILike(x.TaxNumber!, pattern));
        }

        if(request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderBy(x => x.CompanyName);
        }

        return query.ToPagedResponseAsync(request, CompanyDto.Projection, cancellationToken);
    }
}

internal sealed record GetCompany(int Id) : IQuery<CompanyDto>;

internal sealed class GetCompanyHandler : IRequestHandler<GetCompany, CompanyDto>
{
    private readonly CrmDbContext _db;

    public GetCompanyHandler(CrmDbContext db) => _db = db;

    public async Task<CompanyDto> Handle(GetCompany request, CancellationToken cancellationToken) =>
        await _db.Companies.Where(x => x.Id == request.Id).Select(CompanyDto.Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Company", request.Id);
}

// ---------------------------------------------------------------- Create / update / delete

internal sealed record CreateCompany(string CompanyName, string? TaxNumber) : ICommand<CompanyDto>;

internal sealed class CreateCompanyValidator : AbstractValidator<CreateCompany>
{
    public CreateCompanyValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TaxNumber).MaximumLength(11);
    }
}

internal sealed class CreateCompanyHandler : IRequestHandler<CreateCompany, CompanyDto>
{
    private readonly CrmDbContext _db;

    public CreateCompanyHandler(CrmDbContext db) => _db = db;

    public async Task<CompanyDto> Handle(CreateCompany request, CancellationToken cancellationToken)
    {
        var taxNumber = CompanyRules.Normalize(request.TaxNumber);
        await CompanyRules.EnsureTaxNumberIsFreeAsync(_db, taxNumber, excludeId: null, cancellationToken);

        var company = new Company { CompanyName = request.CompanyName.Trim(), TaxNumber = taxNumber };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(cancellationToken);

        return CompanyDto.Compiled(company);
    }
}

/// <summary>Only non-null fields are changed. An empty <see cref="TaxNumber"/> clears it.</summary>
internal sealed record UpdateCompany(int Id, string? CompanyName, string? TaxNumber, bool? IsActive) : ICommand<CompanyDto>;

internal sealed class UpdateCompanyValidator : AbstractValidator<UpdateCompany>
{
    public UpdateCompanyValidator()
    {
        RuleFor(x => x.CompanyName!).NotEmpty().MaximumLength(100).When(x => x.CompanyName is not null);
        RuleFor(x => x.TaxNumber).MaximumLength(11);
    }
}

internal sealed class UpdateCompanyHandler : IRequestHandler<UpdateCompany, CompanyDto>
{
    private readonly CrmDbContext _db;

    public UpdateCompanyHandler(CrmDbContext db) => _db = db;

    public async Task<CompanyDto> Handle(UpdateCompany request, CancellationToken cancellationToken)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Company", request.Id);

        if(request.TaxNumber is not null)
        {
            var taxNumber = CompanyRules.Normalize(request.TaxNumber);
            await CompanyRules.EnsureTaxNumberIsFreeAsync(_db, taxNumber, company.Id, cancellationToken);
            company.TaxNumber = taxNumber;
        }

        if(request.CompanyName is not null) company.CompanyName = request.CompanyName.Trim();
        if(request.IsActive.HasValue) company.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(cancellationToken);

        return await _db.Companies.Where(x => x.Id == company.Id).Select(CompanyDto.Projection).SingleAsync(cancellationToken);
    }
}

internal sealed record DeleteCompany(int Id) : ICommand;

internal sealed class DeleteCompanyHandler : IRequestHandler<DeleteCompany, Unit>
{
    private readonly CrmDbContext _db;

    public DeleteCompanyHandler(CrmDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteCompany request, CancellationToken cancellationToken)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Company", request.Id);

        if(await _db.Customers.AnyAsync(x => x.CompanyId == company.Id, cancellationToken))
        {
            throw new ConflictException("The company has customers and cannot be deleted.", "company.has_customers");
        }

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal static class CompanyRules
{
    public static string? Normalize(string? taxNumber) =>
        string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();

    public static async Task EnsureTaxNumberIsFreeAsync(CrmDbContext db, string? taxNumber, int? excludeId, CancellationToken cancellationToken)
    {
        if(taxNumber is not null &&
           await db.Companies.AnyAsync(x => x.TaxNumber == taxNumber && x.Id != excludeId, cancellationToken))
        {
            throw new ConflictException("A company with the same tax number already exists.", "company.tax_number_taken");
        }
    }
}
