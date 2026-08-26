# AttendanceSystem

نظام حضور وانصراف للعمال باستخدام ASP.NET Core 8 MVC + Web API + EF Core + PostgreSQL/Supabase، مع Razor Views وBootstrap/JavaScript في نفس التطبيق.

## Features

- Worker flow: Employee Code → browser location permission → server-side geofence → Check In / Check Out.
- `GET /api/attendance/status` restores the worker's current state after refresh.
- No continuous GPS tracking.
- Server UTC time is authoritative.
- Server-side Haversine geofence and GPS accuracy validation.
- Duplicate protection at UI, application, and PostgreSQL levels.
- Unique `RequestId` idempotency reservation for every check-in/check-out attempt.
- PostgreSQL partial unique index guarantees one open attendance per employee.
- Every accepted/rejected attempt is stored in `attendance_attempts`.
- ASP.NET Core Identity + secure cookie authentication for Admin.
- CSRF protection on Admin write APIs.
- Employees / Work Sites / Attendance / Rejected Attempts / Reports dashboard APIs.
- HTTPS redirection, HSTS, forwarded headers, rate limiting, global JSON error handling.
- No Docker required.

## Requirements

- .NET 8 SDK
- Supabase project with PostgreSQL connection string
- `dotnet-ef` tool

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

## Environment variables

Never commit production secrets to GitHub.

```text
ConnectionStrings__DefaultConnection=Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=StrongPassword123
ADMIN_FULL_NAME=System Admin
ASPNETCORE_ENVIRONMENT=Production
AUTO_MIGRATE=false
```

For Supabase, prefer the connection string shown in the Supabase dashboard. If your host does not support direct IPv6 database connections, use the Supabase Session/Transaction Pooler connection string provided by the dashboard instead of inventing the hostname/port manually.

## Create/update database

An initial EF migration is included.

```bash
dotnet restore
dotnet ef database update
```

The migration creates Identity tables plus:

- `work_sites`
- `employees`
- `attendance_records`
- `attendance_attempts`

Important database constraints include:

```sql
CREATE UNIQUE INDEX ux_employee_open_attendance
ON attendance_records ("EmployeeId")
WHERE "CheckOutTimeUtc" IS NULL;
```

and a unique index on `attendance_attempts."RequestId"`.

## Run locally

Use HTTPS because browser geolocation is designed for secure contexts in production.

```bash
dotnet restore
dotnet build
dotnet ef database update
dotnet run --launch-profile https
```

Open the HTTPS URL printed by ASP.NET Core.

Routes:

```text
/
/worker/checkin
/admin/login
/admin/dashboard
/admin/employees
/admin/sites
/admin/attendance
/admin/rejected-attempts
/admin/reports
/swagger        Development only
/health
```

Development seed creates `EMP-1025` and `EMP-1026` only when no sites exist. Production never creates demo workers.

## Worker APIs

```text
GET  /api/attendance/status?employeeCode=EMP-1025
POST /api/attendance/checkin
POST /api/attendance/checkout
```

Example body:

```json
{
  "requestId": "7b2d5897-03da-44db-8c1d-21a257c98421",
  "employeeCode": "EMP-1025",
  "latitude": 24.08892,
  "longitude": 32.89981,
  "accuracy": 8.4
}
```

The browser never decides whether the worker is inside the site. It only sends fresh coordinates and accuracy. The server loads the assigned site and calculates the distance.

## Duplicate-safety design

A check-in is protected by several independent controls:

1. The Frontend disables the button during the request.
2. `RequestId` is reserved in `attendance_attempts` before business processing.
3. A second request with the same RequestId returns `DUPLICATE_REQUEST`.
4. The service checks whether an open attendance already exists.
5. PostgreSQL itself prevents two records with `CheckOutTimeUtc IS NULL` for the same employee.
6. A PostgreSQL unique-constraint race is translated into `ALREADY_CHECKED_IN` rather than exposing a database error.

This means concurrent requests cannot create two open attendances even if both pass an application-level check at almost the same moment.

## Admin authentication

The initial Admin is created from `ADMIN_EMAIL`, `ADMIN_PASSWORD`, and `ADMIN_FULL_NAME`. No default production password exists in source code.

Admin API authentication uses an HttpOnly/Secure cookie. API endpoints do not require antiforgery tokens.

## HTTPS / reverse proxy

Production pipeline uses forwarded headers, HTTPS redirection, HSTS, and Secure cookies. The code accepts one forwarded proxy hop. For a production host that documents fixed proxy IP ranges, restrict `KnownNetworks/KnownProxies` to those ranges rather than trusting arbitrary forwarding headers.

## Publish without Docker

```bash
dotnet publish -c Release -o ./publish
```

Upload the contents of `publish/` to an ASP.NET Core 8 compatible host, then configure the environment variables in the host control panel. If the host gives you no shell for `dotnet ef database update`, temporarily set `AUTO_MIGRATE=true` for the first deployment so the included migration is applied on startup; after a successful migration, set it back to `false`.

Do **not** upload a plaintext Supabase password or production `appsettings` secrets into a public GitHub repository.

## Supabase architecture

```text
Browser
   ↓ HTTPS
ASP.NET Core 8
   ↓ EF Core / Npgsql
Supabase PostgreSQL
```

The browser does not contain a Supabase service-role key and does not talk directly to PostgreSQL.

## Production notes

- The current attendance-day grouping uses UTC to stay consistent with server-authoritative UTC timestamps. If business rules later require Egypt-local shifts around midnight, add an explicit `TimeZoneId` to `WorkSite`/`Shift` rather than relying on the server machine's local timezone.
- The current MVP assigns one WorkSite to each Employee. The schema keeps `WorkSiteId` on historical attendance records so moving an employee later does not rewrite history.
- For multiple shifts, add a Shift entity instead of making `(EmployeeId, AttendanceDate)` unique. The only hard uniqueness rule today is one **open** attendance at a time.

## Frontend / Backend endpoint binding

The Razor frontend and Web API are intentionally deployed as one ASP.NET Core application. Frontend API URLs are same-origin relative URLs under `/api`, so there is no hard-coded localhost or production host and no separate CORS configuration is required.

All frontend endpoint paths are centralized in:

```text
wwwroot/js/api-endpoints.js
```

The configured paths map directly to the ASP.NET Core API controllers. When the app is deployed, the same frontend build automatically calls the API on the deployed domain.
