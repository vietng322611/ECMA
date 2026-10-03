# Persistence

## Current state

PostgreSQL is required by the SRS; EF Core is required by the project brief. Neither is wired up. There is no DbContext, entity mapping, migration, connection string, Npgsql/EF package, seed/reset utility, or database service in `compose.yaml`. There is no implemented naming/relationship/transaction convention to preserve yet. The SRS §6 is a conceptual data model, not an existing schema.

Inspection found global `dotnet-ef` 10.0.10, but no repository tool manifest. A global tool on one workstation does not make database commands reproducible elsewhere.

## Minimal first persistence change

Before implementation, inspect again for an existing context. If still absent, the recommended default is `Data/EcmaDbContext.cs`, domain entities in `Models/`, and migrations in `Data/Migrations/`, within the existing project. These paths are recommendations, not current code. Use EF Core 10-compatible packages, `Npgsql.EntityFrameworkCore.PostgreSQL`, and `Microsoft.EntityFrameworkCore.Design`; verify exact compatible versions before installing. Register the context with DI and PostgreSQL configuration in `Program.cs`. Establish a local EF tool manifest/version when migration work first requires it.

Use DataAnnotations or Fluent API in the context for small mappings; extract `IEntityTypeConfiguration<T>` only when mapping size warrants it. Record actual chosen paths/packages here after implementation. Do not add repositories or Unit of Work over DbContext without a concrete need.

Keep connection credentials outside version control (environment variables or configured user secrets). Document the eventual connection-string key and development database setup, but do not claim either exists now.

## Data invariants

- Instants (event/sale/check-in/expiry/creation times) are stored as UTC and presented as Asia/Ho_Chi_Minh (UTC+7). Birth dates are calendar dates, not UTC instants. Exact CLR/store types remain undecided.
- VND amounts are integers. Choose a type that can hold totals, not only the 100,000,000 VND ticket-price maximum. Capture purchase unit prices for history/reporting.
- The SRS specifies lowercase usernames with case-insensitive uniqueness/login, random globally unique `TKT-` codes, fixed tags, and unique EventTag/EventStaff pairs. Enforce relevant invariants in the database, not only application checks.
- SQL table/column casing, key types, enum representation, and deletion/cascade conventions are not established. The SRS snake_case property examples are not proof of implemented EF naming.
- Review required/optional fields, FKs, unique constraints, query indexes, capacity accounting, delete behavior, and concurrency for every schema change. Event deletion is soft deletion per FR-EVT-07.
- NFR-08 requires transactional Order/Ticket/inventory operations; BR-REG-04 requires no overselling under concurrent requests. An EF transaction alone does not prove race safety. Choose and test locking/conditional updates/concurrency controls against PostgreSQL, including Pending holds and release/idempotency paths.

## Migration workflow (conditional, not runnable yet)

Only after packages, tools, DbContext, and connection configuration exist, run from the root. Replace `FeatureName` with a meaningful migration name; these commands assume the recommended paths/context were adopted. Otherwise derive arguments from the implemented context.

```text
dotnet tool restore
dotnet ef migrations add FeatureName --project ECMA.csproj --startup-project ECMA.csproj --context EcmaDbContext --output-dir Data/Migrations
dotnet ef migrations script --idempotent --project ECMA.csproj --startup-project ECMA.csproj --context EcmaDbContext
dotnet build ECMA.sln
dotnet test ECMA.sln
dotnet ef database update --project ECMA.csproj --startup-project ECMA.csproj --context EcmaDbContext
```

`dotnet tool restore` requires the future local manifest. Inspect migration code, snapshot, and SQL before application. Apply only to an authorized development database; check the command result and database migration history/state. If PostgreSQL is unavailable, report application/integration verification as blocked. Never equate migration generation with application. Back up and obtain approval for destructive changes; do not edit migrations already shared/applied to bypass history. Document a safe rollback or forward-fix plan for significant schema changes.