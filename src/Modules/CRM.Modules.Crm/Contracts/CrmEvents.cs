using CRM.BuildingBlocks.Messaging;

namespace CRM.Modules.Crm.Contracts;

public sealed record CompanyCreated(int CompanyId, string CompanyName) : IntegrationEvent;

public sealed record CompanyDeleted(int CompanyId) : IntegrationEvent;

public sealed record CustomerCreated(int CustomerId, int CompanyId, string Email) : IntegrationEvent;

public sealed record CustomerDeleted(int CustomerId, int CompanyId) : IntegrationEvent;
