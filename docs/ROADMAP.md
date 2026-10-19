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

## Phase 2 – UI rewrite (Angular + PrimeNG)

1. New Angular app with PrimeNG + PrimeIcons, theme (light/dark), Transloco (tr/en, user preference).
2. Typed API client generated from OpenAPI.
3. Shared UI kit: page header/breadcrumb, data table (server-side paging/filter/sort), form fields, empty/loading states, confirm dialog, toasts, problem-details error mapping.
4. App shell: collapsible sidebar driven by permissions, topbar with user menu and language switch.
5. Screens: login / register, dashboard, companies (list + detail with tabs), customers (list + detail), users & roles (role + permission assignment), profile (preferences, change password).
6. Playwright e2e tests, UI build in CI.

## Phase 3 – New CRM modules

Activities (calls, meetings, notes), deals / pipeline (kanban), tasks, notifications, attachments, reporting.
Each one is a new `src/Modules/CRM.Modules.<Name>` project registered in the host.

## Adding a module

1. Create `src/Modules/CRM.Modules.<Name>` referencing `CRM.BuildingBlocks`.
2. Implement `IModule` (services, endpoints, permissions) and a `ModuleDbContext` with its own schema.
3. Put anything other modules may use under the `Contracts` namespace (integration events, public queries).
4. Register it in `CRM.API/Program.cs` (`builder.AddModules(...)`).
5. Architecture tests fail if a module references another module's internals.
