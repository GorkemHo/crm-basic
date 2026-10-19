# CRM.UI

Angular 22 + PrimeNG 22 front end for the CRM API.

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
