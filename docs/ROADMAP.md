# Roadmap

Decisions (2026-10-19):

- **Backend first**, then the UI.
- **Modular monolith**: one project per module. A module only exposes what is in its `Contracts` namespace.
- **UI: Angular Material** (MIT) replaces DevExtreme. PrimeNG was tried but since v19+ it requires a PrimeUI license key (commercial, with a conditional free tier), so it was dropped too.
- **Language**: default `tr`, configurable per user (`preferred_language`).

Decisions (2026-10-20):

- **Single tenant.** Every customer runs their own deployment on their own server, so there is no tenant id, tenant filter or tenant resolution anywhere, and none should be added.
- **Deal stages are manageable**: admins add, rename and reorder stages and set a win probability per stage (no hard-coded enum).
- **Multi-currency** amounts: TRY, USD, EUR (amount + currency code on each money value).
- **Deferred**: record owner / "my records" scoping, role-scoped data visibility, Dockerfile for the UI. (Task assignees came with the Tasks module; a general owner field on companies and deals is still open.)

## Definition of done (every feature)

A feature is finished only when all of these are in the same change set:

1. Backend: slice (request, validator, handler), endpoint with `RequirePermission`, migration if the schema changed.
2. Tests: unit tests for domain rules, integration tests for the endpoints (success, validation, permission, not found).
3. UI screen(s) and Playwright e2e tests for the main flow.
4. **Demo data**: the module's `SeedDemoDataAsync` creates realistic sample records for the feature through the module's own commands, idempotently, and `DemoDataTests` asserts them.
5. Docs: this roadmap and the README (configuration, API overview) updated.

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

## Phase 2 – UI rewrite (Angular 22 + Angular Material 22)

`CRM.UI/`: standalone components, signals, zoneless; Material 3 theme (light/dark), Material Symbols icons; Transloco (tr default, en). E2E tests: Playwright.
The dev server proxies `/api` to the API, so the browser talks to one origin.

### Foundation

| # | Step | Status |
|---|---|---|
| 1 | Core: auth store (claims from the access token), interceptor (bearer, `Accept-Language`, refresh + retry), guards (auth, guest, permission), problem-details → toast / form-field errors, i18n with per-user language, theme | ✅ |
| 2 | Layout: auth layout (login/register), app shell with permission-driven sidebar, topbar (language, theme, user menu), 404 / 403 pages | ✅ |
| 3 | Shared UI: page header, server-side table helpers (lazy paging/sort, debounced search, filters), confirm-delete, history timeline (audit log), status tag, date formatting by language | ✅ |

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

All screens above are implemented (✅) and covered by Playwright e2e tests, which also run in CI against a real API and PostgreSQL.

### Later

- Generated API client from OpenAPI (currently hand-written typed services).
- Dockerfile for the UI (static build behind nginx, proxying `/api`) – deferred.
- Record owner field and "my records" filters – deferred.

## Phase 3 – New CRM modules

Each one is a new `src/Modules/CRM.Modules.<Name>` project registered in the host, built to the definition of done above (including demo data).

| # | Module | Contents | Status |
|---|---|---|---|
| 1 | **Sales** (`sales` schema) | Manageable pipeline stages (order, win probability), deals with company + contact, amount in TRY/USD/EUR, move / win / lose / reopen, kanban board with per-currency totals and weighted value, deal list, company "Deals" tab, dashboard pipeline card | ✅ |
| 2 | **Activities** (`activities` schema) | Notes, calls, meetings and emails on a company, optionally a contact and a deal; authors edit/delete their own (`activities.activities.manage` for anyone's); timeline tabs on company, customer and deal pages, activity list with filters, latest activities on the dashboard | ✅ |
| 3 | **Tasks** (`tasks` schema) | To-dos with due date, priority and an assignee (any active user; defaults to the creator), optionally about a company / contact / deal; complete / reopen; the assignee, the creator or `tasks.tasks.manage` may change a task; "my tasks" page with Mine / Everyone, tabs on company, customer and deal pages, "my tasks" card on the dashboard (overdue / due today); `TaskAssigned` event for future notifications | ✅ |
| 4 | **Notifications** (`notifications` schema) | In-app notifications from other modules' events: task assigned (assignee), task completed (creator), deal won / lost (creator); never for your own action, idempotent per event; text built by the UI in the reader's language; bell with unread count (polled every 30 s and on tab focus) and a notifications page | ✅ |
| 5 | **Attachments** (`attachments` schema) | Files on companies, customers and deals: upload (button or drag and drop, with progress), download, delete by the uploader or `attachments.files.manage`; bytes in a file store (local disk, `Attachments:StoragePath`), allow-listed types, size limit, always downloaded as attachments | ✅ |
| 6 | Reporting | | later |

Notification follow-ups (not built yet): real-time push (SignalR) instead of polling, email delivery, per-user preferences, clean-up of old read notifications, due-date reminders.

Cross-module rules used by Sales (reuse them for the next modules):

- Other modules' records are referenced by id; names come from the owner's Contracts (`ICrmDirectory`, `ISalesDirectory`, `IUserDirectory`), never from its tables.
- To stop deletion of a record you reference, register an `IReferenceCheck` (e.g. a company with deals → `company.has_deals`).
- Amounts in different currencies are never added together; totals are per currency.
- "Today" / "overdue" are the user's calendar day: the UI sends its local date (`today`, `dueTo`) instead of the server guessing a time zone.
- Startup seeding has no signed-in user and counts as the system for author / assignee rules; every endpoint still requires a permission.

## Adding a module

1. Create `src/Modules/CRM.Modules.<Name>` referencing `CRM.BuildingBlocks`.
2. Implement `IModule` (services, endpoints, permissions) and a `ModuleDbContext` with its own schema.
3. Put anything other modules may use under the `Contracts` namespace (integration events, public queries).
4. Register it in `CRM.API/Program.cs` (`builder.AddModules(...)`).
5. Implement `SeedDemoDataAsync` with demo records and cover them in `DemoDataTests`.
6. Architecture tests fail if a module references another module's internals.
