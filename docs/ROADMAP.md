# Roadmap

Decisions (2026-10-19):

- **Backend first**, then the UI.
- **Modular monolith**: one project per module. A module only exposes what is in its `Contracts` namespace.
- **UI: PrimeNG** replaces DevExtreme (DevExtreme is commercial; this repo is public).
- **Language**: default `tr`, configurable per user (`preferred_language`).

## Phase 1 – Modular backend

| # | Step | Status |
|---|---|---|
| 1 | `src/` + `tests/` layout, `CRM.BuildingBlocks` (kernel, exceptions, security, paging, module contract, dispatcher, validation, persistence base) | ✅ |
| 2 | **Identity** module: users, roles, auth, refresh tokens, `preferred_language`, own `identity` schema | ✅ |
| 3 | **Crm** module: companies, customers, own `crm` schema; host composes modules; legacy projects removed | ✅ |
| 4 | Auditing (`created_by`/`updated_by`), soft delete, audit log, applied to every module automatically | ✅ |
| 5 | Permission-based authorization, modules declare their permissions | ✅ |
| 6 | Outbox + cross-module integration events (RabbitMQ when enabled) | ✅ |
| 7 | Serilog, OpenTelemetry, health checks, rate limiting, Dockerfile + docker-compose | ✅ |
| 8 | Unit, architecture (module boundaries) and integration tests; GitHub Actions CI | ✅ |

## Phase 2 – UI rewrite (Angular 22 + PrimeNG 22)

`CRM.UI/`: standalone components, signals, zoneless; PrimeNG Aura theme (light/dark); Transloco (tr default, en).
The dev server proxies `/api` to the API, so the browser talks to one origin.

### Foundation

| # | Step | Status |
|---|---|---|
| 1 | Core: auth store (claims from the access token), interceptor (bearer, `Accept-Language`, refresh + retry), guards (auth, guest, permission), problem-details → toast / form-field errors, i18n with per-user language, theme | ✅ |
| 2 | Layout: auth layout (login/register), app shell with permission-driven sidebar, topbar (language, theme, user menu), 404 / 403 pages | ✅ |
| 3 | Shared UI: page header, server-side table helpers (lazy paging/sort, debounced search, filters), confirm-delete, history timeline (audit log), status tag, date formatting by language | ⬜ |

### Screens

| Route | Screen | Permission | Contents |
|---|---|---|---|
| `/login`, `/register` | Sign in / sign up | anonymous | Validation, API errors in the user's language |
| `/` | Dashboard | signed in | KPI cards (companies, customers, active counts), latest companies and customers, quick actions |
| `/companies` | Company list | `crm.companies.read` | Search, active filter, sortable paged table, create/edit dialog, delete with confirmation |
| `/companies/:id` | Company detail | `crm.companies.read` | Tabs: overview (edit, activate/deactivate), customers of the company (add customer here), history |
| `/customers` | Customer list | `crm.customers.read` | Search, company and active filters, table, create/edit dialog, delete |
| `/customers/:id` | Customer detail | `crm.customers.read` | Overview with company link, edit, history |
| `/users` | User list | `identity.users.read` | Search, role and active filters, create dialog (with roles), edit, delete |
| `/users/:id` | User detail | `identity.users.read` | Overview, role assignment, activate/deactivate, history |
| `/roles` | Role list | `identity.roles.read` | User/permission counts, create, rename, activate, delete |
| `/roles/:id` | Role permissions | `identity.roles.read` | Permission matrix grouped by module and resource; Admin shown read-only |
| `/profile` | My profile | signed in | Account info, roles and permissions, language, theme, change password |

Buttons for actions the user is not allowed to do are hidden (the API enforces the same rules).

### Later

- Playwright e2e tests; UI build in CI.
- Generated API client from OpenAPI (currently hand-written typed services).

## Phase 3 – New CRM modules

Activities (calls, meetings, notes), deals / pipeline (kanban), tasks, notifications, attachments, reporting.
Each one is a new `src/Modules/CRM.Modules.<Name>` project registered in the host.

## Adding a module

1. Create `src/Modules/CRM.Modules.<Name>` referencing `CRM.BuildingBlocks`.
2. Implement `IModule` (services, endpoints, permissions) and a `ModuleDbContext` with its own schema.
3. Put anything other modules may use under the `Contracts` namespace (integration events, public queries).
4. Register it in `CRM.API/Program.cs` (`builder.AddModules(...)`).
5. Architecture tests fail if a module references another module's internals.
