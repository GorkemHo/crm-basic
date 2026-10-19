# CRM.UI

Angular 22 + Angular Material 22 front end for the CRM API.

```bash
pnpm install
pnpm start        # http://localhost:4200, proxies /api to https://localhost:7156
pnpm build        # production build in dist/
```

Start the API first (`dotnet run --project src/Host/CRM.API --launch-profile https` from the repository root).

## Structure

```
src/app/
  core/       auth (token store, interceptor, guards, permissions), i18n, API models, notifications, theme
  layout/     auth layout, app shell (permission-driven sidebar, topbar)
  shared/     page header, field errors, status tag, history timeline
  features/   one folder per area (companies, customers, users, roles, profile, dashboard)
public/i18n/  tr.json, en.json (API error codes are translated under "errors")
```

## E2E tests

With the API and `pnpm start` running:

```bash
npx playwright test        # uses the installed Chrome locally
```

`E2E_EMAIL` / `E2E_PASSWORD` select the admin account (default: the dev seed admin).
