using CRM.BuildingBlocks.Application;
using CRM.BuildingBlocks.Modules;
using CRM.BuildingBlocks.Pagination;
using CRM.BuildingBlocks.Persistence;
using CRM.Modules.Crm.Features.Companies;
using CRM.Modules.Crm.Features.Customers;
using CRM.Modules.Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CRM.Modules.Crm;

/// <summary>Companies and their customers.</summary>
public sealed class CrmModule : IModule
{
    internal const string AccessPolicy = "crm.access";

    public string Name => CrmDbContext.SchemaName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.SchemaName);
        services.AddRequestHandlersFromAssembly(typeof(CrmModule).Assembly);

        services.AddAuthorizationBuilder()
            .AddPolicy(AccessPolicy, policy => policy.RequireRole("Admin", "User"));
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        var companies = api.MapGroup("/companies").WithTags("Companies").RequireAuthorization(AccessPolicy);

        companies.MapGet("", async ([AsParameters] GetCompanies query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        companies.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetCompany(id), ct)));

        companies.MapPost("", async Task<Created<CompanyDto>> (CreateCompany request, ISender sender, CancellationToken ct) =>
        {
            var company = await sender.Send(request, ct);
            return TypedResults.Created($"/api/companies/{company.Id}", company);
        });

        companies.MapPatch("/{id:int}", async (int id, UpdateCompanyBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new UpdateCompany(id, body.CompanyName, body.TaxNumber, body.IsActive), ct)));

        companies.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCompany(id), ct);
            return TypedResults.NoContent();
        });

        var customers = api.MapGroup("/customers").WithTags("Customers").RequireAuthorization(AccessPolicy);

        customers.MapGet("", async ([AsParameters] GetCustomers query, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(query, ct)));

        customers.MapGet("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new GetCustomer(id), ct)));

        customers.MapPost("", async Task<Created<CustomerDto>> (CreateCustomer request, ISender sender, CancellationToken ct) =>
        {
            var customer = await sender.Send(request, ct);
            return TypedResults.Created($"/api/customers/{customer.Id}", customer);
        });

        customers.MapPatch("/{id:int}", async (int id, UpdateCustomerBody body, ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(
                new UpdateCustomer(id, body.FirstName, body.LastName, body.Email, body.PhoneNumber, body.CompanyId, body.IsActive), ct)));

        customers.MapDelete("/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCustomer(id), ct);
            return TypedResults.NoContent();
        });
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        if(scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync(cancellationToken);
        }
    }

    internal sealed record UpdateCompanyBody(string? CompanyName, string? TaxNumber, bool? IsActive);

    internal sealed record UpdateCustomerBody(
        string? FirstName, string? LastName, string? Email, string? PhoneNumber, int? CompanyId, bool? IsActive);
}
