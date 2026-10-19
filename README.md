# crm-basic

A basic CRM: companies, customers, users and roles.

| Project | Description |
|---|---|
| `CRM.API` | ASP.NET Core (.NET 10) Web API, JWT auth, OpenAPI + Scalar UI in Development |
| `CRM.Core` | Entities, DTOs, mappings, repository/service interfaces, messaging abstractions |
| `CRM.Infrastructure` | EF Core (PostgreSQL), repositories, services, JWT, RabbitMQ |
| `CRM.Tests` | xUnit + NSubstitute unit tests |
| `CRM.UI` | Angular 22 + DevExtreme front end |

## Requirements

- .NET 10 SDK
- PostgreSQL
- Node 24 and pnpm (for the UI)

## Backend setup

Secrets are not stored in `appsettings.json`. For local development use user-secrets:

```bash
cd CRM.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CRMDB;Username=postgres;Password=<password>"
dotnet user-secrets set "JwtSettings:SecretKey" "<random string, at least 32 bytes>"
```

In other environments use environment variables (`ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`). The API refuses to start without a JWT key.

Allowed CORS origins are configured under `Cors:AllowedOrigins` (Development allows `http://localhost:4200`).

### Database

The schema is managed with EF Core migrations (`CRM.Infrastructure/Persistence/Migrations`).

**New database:**

```bash
dotnet tool restore --tool-manifest CRM.API/dotnet-tools.json
dotnet ef database update --project CRM.Infrastructure --startup-project CRM.API
```

Then insert the roles (`Admin`, `User`) and promote your first user to `Admin`:

```sql
INSERT INTO roles (name) VALUES ('Admin'), ('User');
-- after registering through the UI / POST /api/auth/register:
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id FROM users u, roles r WHERE u.email = 'you@example.com' AND r.name = 'Admin';
```

**Database created before migrations existed:** `InitialCreate` mirrors that hand-made schema, so mark it as applied and run only the later migrations:

```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
INSERT INTO "__EFMigrationsHistory" VALUES ('20261019094009_InitialCreate', '10.0.10');
```

```bash
dotnet ef database update --project CRM.Infrastructure --startup-project CRM.API
```

### Run

```bash
dotnet run --project CRM.API --launch-profile https
```

API reference (Development): `https://localhost:7156/scalar`. Sample requests: `CRM.API/CRM.API.http`.

### Tests

```bash
dotnet test
```

## Authentication

- `POST /api/auth/register` creates a user with the `User` role. Other roles are assigned by an Admin (`POST /api/users/{id}/roles`).
- `POST /api/auth/login` returns a short-lived access token and a refresh token.
- `POST /api/auth/refresh` exchanges a refresh token for a new pair. Each refresh token works once; reusing a revoked one revokes all of that user's sessions.
- `POST /api/auth/logout` revokes a refresh token.
- `GET /api/auth/me` returns the current user.

Only SHA-256 hashes of refresh tokens are stored. Lifetimes: `JwtSettings:ExpirationMinutes` and `JwtSettings:RefreshTokenExpirationDays`.

## Messaging (RabbitMQ)

Messaging is **disabled by default** (`RabbitMq:Enabled = false`). While disabled, `IMessagePublisher` is a no-op, so code can publish messages without checking whether a broker exists.

To enable it, set `RabbitMq:Enabled` to `true`, configure the host, and set the password with `dotnet user-secrets set "RabbitMq:Password" "<password>"`.

Publish a message (it goes to the `crm.events` topic exchange; the routing key defaults to the type name, e.g. `CustomerCreated` → `customer.created`):

```csharp
public record CustomerCreated(int CustomerId, string Email);

await _messagePublisher.PublishAsync(new CustomerCreated(customer.Id, customer.Email));
```

Consume it by implementing `IMessageHandler<T>` and registering it after `AddInfrastructure`:

```csharp
public class SendWelcomeMail : IMessageHandler<CustomerCreated>
{
    public Task HandleAsync(CustomerCreated message, CancellationToken cancellationToken) { /* ... */ }
}

builder.Services.AddMessageConsumer<CustomerCreated, SendWelcomeMail>(
    builder.Configuration, queueName: "crm.customer-created.welcome-mail");
```

Each consumer declares a durable queue bound to the exchange. Every message gets its own DI scope. A message is acked on success; if the handler throws, it is nacked and not requeued.

## UI

```bash
cd CRM.UI
pnpm install
pnpm start        # http://localhost:4200
```

The API base URL is set in `src/environments/environment.ts`. Roles are read from the access token. Expired access tokens are refreshed automatically.
