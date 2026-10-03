# ECMA agent instructions

## Project and source of truth

ECMA manages events/conferences, ticket orders, mock payments, check-in, and role-scoped reporting. Detailed requirements: [SRS](docs/product/SRS_ECMA.md). Target stack: .NET 10, ASP.NET Core MVC, server-rendered Razor, PostgreSQL, EF Core. This is not a SPA.

Priority: explicit user task > accepted repository decisions > SRS > architecture/convention documents > existing implementation. Identify conflicts; never silently invent behavior. See [accepted SSR decision](docs/decisions/0001-server-rendered-mvc.md) and [open questions/feature map](docs/product/agent-context.md). `FR-05` is not a unique SRS ID; ask for the module-qualified ID.

Accepted choices: [cookie authentication and Identity `PasswordHasher<TUser>`](docs/decisions/0002-cookie-authentication-password-hashing.md), with hashes stored in PostgreSQL; [Razor `.cshtml` and Tailwind CSS v4](docs/decisions/0003-razor-tailwind-styling.md). Identity PBKDF2 explicitly overrides the SRS's bcrypt/argon2 wording. These choices are not yet implemented. Tailwind packages are installed via Bun; Bootstrap remains in the template until scoped migration.

## Current repository

Only an MVC template exists: `Program.cs`, `Controllers/HomeController.cs`, `Models/ErrorViewModel.cs`, `Views/`, `wwwroot/`. No domain entities, DbContext, EF packages/migrations, authentication, business services, or tests exist yet. Do not describe planned infrastructure as implemented. Preserve unrelated user changes, including the existing `ECMA.csproj` folder entries.

Read [overview](docs/architecture/overview.md), [conventions](docs/development/conventions.md), [persistence](docs/architecture/persistence.md), [UI](docs/architecture/ui.md), and [verification](docs/development/verification.md) as relevant. Reinspect code each session; documentation is a baseline, not proof of current state.

## Feature workflow

1. Read the full relevant FR, BR, NFR, permission matrix, and feature dependencies.
2. Inspect existing request flow, DI, persistence, views, and tests. Resolve task-blocking ambiguities before implementing affected behavior.
3. Write a short plan naming affected files, necessary prerequisites, and observable completion criteria. Keep a checklist in the session; no task queue or orchestration is required.
4. Implement the smallest coherent change. Reuse code and native .NET/MVC features before adding dependencies. Do not introduce speculative layers, CQRS, MediatR, generic repositories, or Unit of Work.
5. Build and run relevant tests. Inspect failures, fix their causes, and rerun checks. Add a focused runnable regression test for non-trivial behavior; if no test infrastructure exists, establish only what that feature needs.
6. Verify the actual HTTP/UI flow, permissions, persisted state, and boundary/concurrency cases where relevant. A successful build or test command with zero discovered tests is not feature verification.
7. Review the complete task diff against requirements, security, and acceptance criteria. Preserve unrelated changes. Update documentation when behavior, setup, or significant decisions change.
8. Stop when acceptance criteria have evidence. Report commands/results, executed test count, and any blockers or unverified checks. Never claim completion while a required check failed or could not run.

Planner, implementer, tester, and reviewer are optional perspectives on this workflow, not separate required agents.

## MVC rules

- Controllers handle HTTP; services own non-trivial business logic. Use DI in `Program.cs`, conventional routing, and async EF operations when appropriate.
- Use strongly typed ViewModels for forms and presentation. Do not bind persistence entities when that risks over-posting or exposes persistence concerns.
- Use DataAnnotations and `ModelState` for server validation, plus business-rule checks. Client validation is optional enhancement, never authority.
- Enforce authentication, role policies/attributes, ownership, and Staff event assignments on the server. Hiding buttons is not authorization.
- State changes use POST with antiforgery validation; no mutating GET. Prefer Post/Redirect/Get after success; return the populated form and validation errors after invalid input.
- Razor must encode user content. Do not use `Html.Raw` for descriptions or other untrusted text. Use in-page operation feedback, not email/notifications.
- Do not create APIs to connect Razor to the same application without an explicit need. No React/Vue/Angular/Blazor or separate frontend application by default.

## Database rules

PostgreSQL + EF Core are required but not configured. Before changing persistence, locate the current DbContext/configuration and preserve conventions. When absent, add minimal infrastructure only as a feature prerequisite and document its actual paths.

Schema changes require migrations and review of generated operations and SQL. Consider uniqueness, foreign keys, nullability, indexes, delete behavior, and concurrency. Avoid destructive changes unless explicitly required and approved with a data-preservation plan. Generated migrations are not evidence of application to a database. Never reset/drop a non-disposable database or commit credentials. Store instants in UTC; display Asia/Ho_Chi_Minh; VND amounts are integers.

## Commands

From the repository root, using a .NET 10 SDK:

```text
dotnet restore ECMA.sln
dotnet build ECMA.sln --no-restore
dotnet test ECMA.sln --no-build
```

Currently there are no test projects; the last command may succeed without running tests. See verification documentation for runtime checks and conditional migration prerequisites. Do not downgrade .NET or upgrade unrelated dependencies.

## Optional skills

Skills are not project dependencies. Read installed instructions before use. Apply `ponytail` for reuse and minimal changes without removing security/tests; `caveman` for concise chat, not cryptic persisted documentation. The inspected `design-taste-frontend` identifies itself as `tasteskill`; its landing-page/audit/accessibility advice is optional. Its React/Next defaults and dashboard exclusions do not override MVC/Razor, accepted Tailwind styling, or product requirements. No exact `taste-skill` installation was found in inspected locations; do not assume an alias is installed.