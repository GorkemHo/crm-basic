# crm-basic

A CRM built as a **modular monolith**: ASP.NET Core (.NET 10) + PostgreSQL, with an Angular 22 + Angular Material UI in `CRM.UI/` (see [CRM.UI/README.md](CRM.UI/README.md) and [docs/ROADMAP.md](docs/ROADMAP.md)).

## Layout

```
src/
  BuildingBlocks/CRM.BuildingBlocks   shared kernel: module contract, dispatcher, validation, persistence base,
                                      auditing, soft delete, outbox, messaging, security primitives, paging
  Modules/CRM.Modules.Identity        users, roles, permissions, JWT + refresh tokens        (schema: identity)
  Modules/CRM.Modules.Crm             companies, customers                                   (schema: crm)
  Host/CRM.API                        composition root: error handling, observability, health, OpenAPI
tests/
  CRM.UnitTests                       building blocks, token service
  CRM.ArchitectureTests               module boundary rules (NetArchTest)
  CRM.IntegrationTests                real API + real PostgreSQL
```

### Module rules

- One project per module. Everything is `internal` except the `IModule` class and the `Contracts` namespace.
- A module may use another module **only through its `Contracts`** (integration events, permission names, public queries). The architecture tests enforce this.
- Each module owns its own `DbContext`, PostgreSQL schema and migrations.
- Features are vertical slices: request + FluentValidation validator + handler in one place, dispatched via `ISender`, exposed as minimal API endpoints.

### What every module gets automatically

| Concern | How |
|---|---|
| Auditing | `AuditableEntity`: `created_at/by`, `updated_at/by` filled on save |
| Soft delete | `SoftDeletableEntity`: delete sets `is_deleted`; global query filter; unique indexes ignore deleted rows |
| Audit log | `audit_logs` per schema: field-level old/new values for updates, snapshot for deletes (`[NotAudited]` excludes fields); `…/{id}/history` endpoints |
| Events | `db.AddIntegrationEvent(...)` / `SaveChangesAndPublishAsync(...)` write to the module's `outbox_messages` in the same transaction; a background processor delivers to `IIntegrationEventHandler<T>`s and to RabbitMQ (if enabled) |
| Authorization | Module declares `PermissionDefinition`s; endpoints use `.RequirePermission(...)` |
| Errors | Throw `AppException` subtypes; the API answers with RFC 7807 problems |
| Validation | FluentValidation validators run before handlers; messages follow `Accept-Language` (en/tr) |

### Adding a module

1. Create `src/Modules/CRM.Modules.<Name>` referencing `CRM.BuildingBlocks` (copy `CRM.Modules.Crm.csproj`).
2. Add a `ModuleDbContext` subclass (own schema) plus a `ModuleDesignTimeDbContextFactory` subclass, then:
   `dotnet ef migrations add InitialCreate --project src/Modules/CRM.Modules.<Name> --output-dir Infrastructure/Persistence/Migrations`
3. Implement `IModule` (`Register`, `MapEndpoints`, `Permissions`, `InitializeAsync` → `ModuleInitialization.MigrateIfEnabledAsync`).
4. Reference it from `CRM.API` and add it to `builder.AddModules(...)` in `Program.cs`.
5. Implement `SeedDemoDataAsync` (demo records created through the module's commands, idempotent) and assert them in `tests/CRM.IntegrationTests/DemoDataTests.cs`.
6. Run `dotnet test`: the architecture tests pick the new module up automatically.

## Running locally

Requirements: .NET 10 SDK, PostgreSQL.

```bash
cd src/Host/CRM.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CRMDB;Username=postgres;Password=<password>"
dotnet user-secrets set "JwtSettings:SecretKey" "<random string, at least 32 bytes>"
dotnet user-secrets set "Seed:AdminEmail" "admin@crm.local"      # optional: creates an admin user
dotnet user-secrets set "Seed:AdminPassword" "<password>"
dotnet user-secrets set "Seed:AdminLanguage" "en"                 # optional, default tr
cd ../../..
dotnet run --project src/Host/CRM.API --launch-profile https
```

In Development the API applies migrations at startup (`Database:MigrateOnStartup`). It always creates the built-in roles (`Admin`, `User`) and syncs the permission catalog.

### Demo data

With `Seed:DemoData=true` (on in Development and in the CI e2e job) every module seeds sample data once at startup: fictional companies and contacts, a *Sales Manager* role and these users (password `Demo1234!`, override with `Seed:DemoPassword`):

| User | Role | Language |
|---|---|---|
| ayse.yilmaz@demo.local | User + Sales Manager (all CRM permissions, can view users) | tr |
| mehmet.kaya@demo.local | User | tr |
| elif.demir@demo.local | User | en |
| can.ozturk@demo.local | User (inactive, cannot sign in) | tr |

A few small demo files are attached to demo deals and companies. Demo tasks are assigned to the demo users, who get a notification for each (from the system) (overdue, due today, upcoming, done). Demo activities (notes, calls, meetings, emails, one planned) are logged on the demo companies and deals. Sales demo deals are spread over the pipeline, with deals won and lost over the last five months for the reports, in TRY, USD and EUR, with some won and lost in the current month. Seeding is idempotent and skipped when the demo records already exist. Keep it off for real deployments.

- API reference: `https://localhost:7156/scalar`; sample requests: `src/Host/CRM.API/CRM.API.http`
- Health: `/health/live`, `/health/ready`

### Docker

```bash
cp .env.example .env    # fill in POSTGRES_PASSWORD, JWT_SECRET_KEY, ADMIN_*
docker compose up --build                       # API on http://localhost:8080
docker compose --profile messaging up --build   # + RabbitMQ (also set RABBITMQ_ENABLED=true)
```

### Tests

```bash
dotnet test
```

Integration tests start PostgreSQL with Testcontainers (Docker required). Without Docker, point them at an existing server; they create and drop a temporary database:

```bash
CRM_TEST_CONNECTION="Host=localhost;Username=postgres;Password=<password>" dotnet test
```

CI (`.github/workflows/ci.yml`) builds, runs all .NET tests, builds the API image, and builds the UI and runs its Playwright e2e tests against a real API + PostgreSQL.

## Configuration

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL (all module schemas live in one database) |
| `JwtSettings:SecretKey` | HMAC signing key, ≥ 32 bytes (required) |
| `JwtSettings:ExpirationMinutes`, `RefreshTokenExpirationDays` | Token lifetimes |
| `Database:MigrateOnStartup` | Apply migrations at startup (default: Development only) |
| `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:AdminLanguage` | Optional initial admin |
| `Seed:DemoData`, `Seed:DemoPassword` | Seed demo data at startup (Development: on) and the demo users' password |
| `Attachments:StoragePath` | Folder for uploaded files (default `App_Data/attachments` under the API). **Back it up together with the database.** |
| `Attachments:MaxFileSizeMb`, `Attachments:AllowedExtensions` | Upload limit (default 20 MB) and allowed file types (no html, svg, scripts or executables) |
| `Cors:AllowedOrigins` | Allowed browser origins |
| `RateLimiting:AuthPermitsPerMinute` | Per-IP limit on login/register/refresh (default 10) |
| `Outbox:PollingIntervalSeconds` | Outbox delivery interval (default 5) |
| `RabbitMq:*` | Broker; `Enabled=false` (default) uses a no-op publisher |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Export traces/metrics over OTLP when set |
| `Serilog:MinimumLevel` | Log levels (console: text in Development, JSON elsewhere) |

## API overview

| Area | Endpoints | Permission |
|---|---|---|
| Auth | `POST /api/auth/login`, `register`, `refresh`, `logout`; `GET /api/auth/me`; `PUT /api/auth/me/preferences`; `POST /api/auth/me/change-password` | anonymous / signed in |
| Users | `GET/POST /api/users`, `GET/PATCH/DELETE /api/users/{id}`, `PUT /api/users/{id}/roles`, `GET /api/users/{id}/history` | `identity.users.read` / `.manage` |
| Roles | `GET/POST /api/roles`, `GET/PATCH/DELETE /api/roles/{id}`, `PUT /api/roles/{id}/permissions`, `GET /api/permissions` | `identity.roles.read` / `.manage` |
| Companies | `GET/POST /api/companies`, `GET/PATCH/DELETE /api/companies/{id}`, `GET /api/companies/{id}/history` | `crm.companies.read` / `.write` / `.delete` |
| Customers | `GET/POST /api/customers`, `GET/PATCH/DELETE /api/customers/{id}`, `GET /api/customers/{id}/history` | `crm.customers.read` / `.write` / `.delete` |
| Pipeline stages | `GET/POST /api/pipeline-stages`, `PATCH/DELETE /api/pipeline-stages/{id}`, `PUT /api/pipeline-stages/order` | `sales.deals.read` / `sales.stages.manage` |
| Activities | `GET/POST /api/activities`, `GET/PUT/DELETE /api/activities/{id}` (filters: `companyId`, `customerId`, `dealId`, `type`, `createdBy`, `search`) | `activities.activities.read` / `.write` (own) / `.manage` (anyone's) |
| Attachments | `GET /api/attachments?recordType=&recordId=`, `POST /api/attachments` (multipart: `recordType` company / customer / deal, `recordId`, `file`), `GET /api/attachments/{id}/download`, `DELETE /api/attachments/{id}` | `attachments.files.read` / `.write` (own) / `.manage` |
| Reports | `GET /api/reports/sales?from=&to=`, `/pipeline`, `/activities?from=&to=`, `/tasks?from=&to=&today=` (days, both ends included; default the last six months) | `reporting.reports.read` |
| Notifications | `GET /api/notifications` (`unreadOnly`), `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all` | signed in (own notifications only) |
| Tasks | `GET/POST /api/tasks`, `GET/PUT/DELETE /api/tasks/{id}`, `POST /api/tasks/{id}/complete`, `reopen`; `GET /api/tasks/assignees`, `my-summary?today=` (filters: `mine`, `assigneeId`, `status`, `dueFrom`, `dueTo`, `companyId`, `customerId`, `dealId`, `search`) | `tasks.tasks.read` / `.write` (own or assigned) / `.manage` |
| Deals | `GET/POST /api/deals`, `GET/PUT/DELETE /api/deals/{id}`, `POST /api/deals/{id}/move`, `win`, `lose`, `reopen`; `GET /api/deals/board`, `summary`, `/{id}/history` | `sales.deals.read` / `.write` / `.delete` |

Lists accept `pageNumber`, `pageSize` (≤ 100), `sortBy`, `sortDirection` and module-specific filters (`search`, `isActive`, `companyId`, …) and return `{ items, pageNumber, pageSize, totalCount, totalPages }`.

**Tokens**: access tokens carry `sub`, `email`, `name`, `given_name`, `family_name`, `locale`, `role` and `perm` claims. `Admin` implicitly has every permission; new permissions marked as default are granted to `User` automatically. Role/permission changes apply at the next sign-in or token refresh. Refresh tokens are single-use (rotation), stored only as SHA-256 hashes; reusing a revoked one ends all of the user's sessions.

**Errors** are `application/problem+json`:

```json
{ "status": 409, "title": "Conflict", "detail": "A company with the same tax number already exists.",
  "code": "company.tax_number_taken", "instance": "POST /api/companies", "traceId": "00-…" }
```

| Exception | Status |
|---|---|
| `ValidationException` (optionally with `errors`) | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `BusinessRuleException` | 422 |
| anything else | 500 (details logged, never returned) |
