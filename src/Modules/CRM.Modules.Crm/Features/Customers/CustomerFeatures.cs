using System.Linq.Expressions;
using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Exceptions;
using CRM.BuildingBlocks.Pagination;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Crm.Domain;
using CRM.Modules.Crm.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CRM.Modules.Crm.Features.Customers;

internal sealed record CustomerDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    int CompanyId,
    string CompanyName,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static readonly Expression<Func<Customer, CustomerDto>> Projection = c => new CustomerDto(
        c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.IsActive,
        c.CompanyId, c.Company.CompanyName, c.CreatedAt, c.UpdatedAt);
}

// ---------------------------------------------------------------- List / get

internal sealed class GetCustomers : PaginationRequest, IQuery<PagedResponse<CustomerDto>>
{
    /// <summary>Matches first name, last name, email or phone (case-insensitive).</summary>
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public int? CompanyId { get; set; }
}

internal sealed class GetCustomersHandler : IRequestHandler<GetCustomers, PagedResponse<CustomerDto>>
{
    private readonly CrmDbContext _db;

    public GetCustomersHandler(CrmDbContext db) => _db = db;

    public Task<PagedResponse<CustomerDto>> Handle(GetCustomers request, CancellationToken cancellationToken)
    {
        var query = _db.Customers.AsNoTracking();

        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern) ||
                EF.Functions.ILike(x.LastName, pattern) ||
                EF.Functions.ILike(x.Email, pattern) ||
                EF.Functions.ILike(x.PhoneNumber!, pattern));
        }

        if(request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if(request.CompanyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == request.CompanyId.Value);
        }

        if(string.IsNullOrWhiteSpace(request.SortBy))
        {
            query = query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName);
        }

        return query.ToPagedResponseAsync(request, CustomerDto.Projection, cancellationToken);
    }
}

internal sealed record GetCustomer(int Id) : IQuery<CustomerDto>;

internal sealed class GetCustomerHandler : IRequestHandler<GetCustomer, CustomerDto>
{
    private readonly CrmDbContext _db;

    public GetCustomerHandler(CrmDbContext db) => _db = db;

    public async Task<CustomerDto> Handle(GetCustomer request, CancellationToken cancellationToken) =>
        await _db.Customers.Where(x => x.Id == request.Id).Select(CustomerDto.Projection).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Customer", request.Id);
}

// ---------------------------------------------------------------- Create / update / delete

internal sealed record CreateCustomer(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    int CompanyId) : ICommand<CustomerDto>;

internal sealed class CreateCustomerValidator : AbstractValidator<CreateCustomer>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
        RuleFor(x => x.CompanyId).GreaterThan(0);
    }
}

internal sealed class CreateCustomerHandler : IRequestHandler<CreateCustomer, CustomerDto>
{
    private readonly CrmDbContext _db;

    public CreateCustomerHandler(CrmDbContext db) => _db = db;

    public async Task<CustomerDto> Handle(CreateCustomer request, CancellationToken cancellationToken)
    {
        await CustomerRules.EnsureEmailIsFreeAsync(_db, request.Email, excludeId: null, cancellationToken);
        await CustomerRules.EnsureCompanyExistsAsync(_db, request.CompanyId, cancellationToken);

        var customer = new Customer
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber,
            CompanyId = request.CompanyId
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAndPublishAsync(() => new CustomerCreated(customer.Id, customer.CompanyId, customer.Email), cancellationToken);

        return await _db.Customers.Where(x => x.Id == customer.Id).Select(CustomerDto.Projection).SingleAsync(cancellationToken);
    }
}

/// <summary>Only non-null fields are changed.</summary>
internal sealed record UpdateCustomer(
    int Id,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    int? CompanyId,
    bool? IsActive) : ICommand<CustomerDto>;

internal sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomer>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.FirstName!).NotEmpty().MaximumLength(100).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName!).NotEmpty().MaximumLength(100).When(x => x.LastName is not null);
        RuleFor(x => x.Email!).NotEmpty().EmailAddress().MaximumLength(150).When(x => x.Email is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
        RuleFor(x => x.CompanyId!.Value).GreaterThan(0).When(x => x.CompanyId.HasValue);
    }
}

internal sealed class UpdateCustomerHandler : IRequestHandler<UpdateCustomer, CustomerDto>
{
    private readonly CrmDbContext _db;

    public UpdateCustomerHandler(CrmDbContext db) => _db = db;

    public async Task<CustomerDto> Handle(UpdateCustomer request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer", request.Id);

        if(request.Email is not null && request.Email != customer.Email)
        {
            await CustomerRules.EnsureEmailIsFreeAsync(_db, request.Email, customer.Id, cancellationToken);
            customer.Email = request.Email.Trim();
        }

        if(request.CompanyId.HasValue && request.CompanyId != customer.CompanyId)
        {
            await CustomerRules.EnsureCompanyExistsAsync(_db, request.CompanyId.Value, cancellationToken);
            customer.CompanyId = request.CompanyId.Value;
        }

        if(request.FirstName is not null) customer.FirstName = request.FirstName.Trim();
        if(request.LastName is not null) customer.LastName = request.LastName.Trim();
        if(request.PhoneNumber is not null) customer.PhoneNumber = request.PhoneNumber.Length == 0 ? null : request.PhoneNumber;
        if(request.IsActive.HasValue) customer.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(cancellationToken);

        return await _db.Customers.Where(x => x.Id == customer.Id).Select(CustomerDto.Projection).SingleAsync(cancellationToken);
    }
}

internal sealed record DeleteCustomer(int Id) : ICommand;

internal sealed class DeleteCustomerHandler : IRequestHandler<DeleteCustomer, Unit>
{
    private readonly CrmDbContext _db;

    public DeleteCustomerHandler(CrmDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteCustomer request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer", request.Id);

        _db.Customers.Remove(customer);
        _db.AddIntegrationEvent(new CustomerDeleted(customer.Id, customer.CompanyId));
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

internal static class CustomerRules
{
    public static async Task EnsureEmailIsFreeAsync(CrmDbContext db, string email, int? excludeId, CancellationToken cancellationToken)
    {
        if(await db.Customers.AnyAsync(x => x.Email == email && x.Id != excludeId, cancellationToken))
        {
            throw new ConflictException("A customer with the same email already exists.", "customer.email_taken");
        }
    }

    public static async Task EnsureCompanyExistsAsync(CrmDbContext db, int companyId, CancellationToken cancellationToken)
    {
        if(!await db.Companies.AnyAsync(x => x.Id == companyId, cancellationToken))
        {
            throw new NotFoundException("Company", companyId);
        }
    }
}
