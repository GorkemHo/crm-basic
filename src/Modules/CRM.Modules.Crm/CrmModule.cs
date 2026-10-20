using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Persistence;
using CRM.BuildingBlocks.Security;
using CRM.Modules.Crm.Contracts;
using CRM.Modules.Crm.Features;
using CRM.Modules.Crm.Features.Companies;
using CRM.Modules.Crm.Features.Customers;
using CRM.Modules.Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Modules.Crm;

/// <summary>Companies and their customers.</summary>
public sealed class CrmModule : IModule
{
    public string Name => CrmDbContext.SchemaName;

    public IEnumerable<PermissionDefinition> Permissions => CrmPermissions.All;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(CrmModule).Assembly);

    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var companies = api.MapGroup("/companies").WithTags("Companies");

        companies.MapGet("", async ([AsParameters] GetCompanies query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(CrmPermissions.CompaniesRead);

        companies.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetCompany(id), ct)))
            .RequirePermission(CrmPermissions.CompaniesRead);

        companies.MapPost("", async Task<Created<CompanyDto>> (CreateCompany request, ISender sender, CancellationToken ct) =>
        {
            var company = await sender.Send(request, ct);
            return TypedResults.Created($"/api/companies/{company.Id}", company);
        })
            .RequirePermission(CrmPermissions.CompaniesWrite);

        companies.MapPatch("/{id:int}", async (int id, UpdateCompanyBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateCompany(id, body.CompanyName, body.TaxNumber, body.IsActive), ct)))
            .RequirePermission(CrmPermissions.CompaniesWrite);

        companies.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCompany(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(CrmPermissions.CompaniesDelete);

        companies.MapGet("/{id:int}/history", async ([AsParameters] GetCompanyHistory query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(CrmPermissions.CompaniesRead);

        var customers = api.MapGroup("/customers").WithTags("Customers");

        customers.MapGet("", async ([AsParameters] GetCustomers query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(CrmPermissions.CustomersRead);

        customers.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetCustomer(id), ct)))
            .RequirePermission(CrmPermissions.CustomersRead);

        customers.MapPost("", async Task<Created<CustomerDto>> (CreateCustomer request, ISender sender, CancellationToken ct) =>
        {
            var customer = await sender.Send(request, ct);
            return TypedResults.Created($"/api/customers/{customer.Id}", customer);
        })
            .RequirePermission(CrmPermissions.CustomersWrite);

        customers.MapPatch("/{id:int}", async (int id, UpdateCustomerBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(
                new UpdateCustomer(id, body.FirstName, body.LastName, body.Email, body.PhoneNumber, body.CompanyId, body.IsActive), ct)))
            .RequirePermission(CrmPermissions.CustomersWrite);

        customers.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCustomer(id), ct);
            return TypedResults.NoContent();
        })
            .RequirePermission(CrmPermissions.CustomersDelete);

        customers.MapGet("/{id:int}/history", async ([AsParameters] GetCustomerHistory query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)))
            .RequirePermission(CrmPermissions.CustomersRead);
    }

    public Task SeedDemoDataAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        CrmDemoData.SeedAsync(scopedServices, cancellationToken);

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        await ModuleInitialization.MigrateIfEnabledAsync<CrmDbContext>(scope.ServiceProvider, cancellationToken);
    }

    internal sealed record UpdateCompanyBody(string? CompanyName, string? TaxNumber, bool? IsActive);

    internal sealed record UpdateCustomerBody(
        string? FirstName, string? LastName, string? Email, string? PhoneNumber, int? CompanyId, bool? IsActive);
}
