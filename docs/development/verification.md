# Verification and feature completion

Run commands from the repository root with a .NET 10 SDK. `ECMA.sln` contains Web, Application, and Domain projects but no test projects. No CI, test runner packages, seed/reset scripts, local tool manifest, or PostgreSQL environment is configured. Keep this document current as infrastructure is introduced.

## Supported baseline commands

```text
dotnet --info
dotnet restore ECMA.sln
dotnet build ECMA.sln --no-restore
dotnet test ECMA.sln --no-build
dotnet run --project src/ECMA.Web/ECMA.Web.csproj --launch-profile http --no-build
```

The HTTP profile serves `http://localhost:5028`; the HTTPS profile uses `https://localhost:7076` and HTTP 5028. HTTP-only development may log a missing HTTPS-port warning because HTTPS redirection is configured; use the HTTPS profile with a trusted development certificate when testing HTTPS. Do not suppress production HTTPS protections.

Use a disposable PostgreSQL database through `ConnectionStrings:ECMA`. Seed with `dotnet run --project src/ECMA.Web -- --seed`; reset and reseed with `--reset-db`. Reset scripts reject database names without `dev` or `test`.

`tests/ECMA.E2E.Tests` is intentionally an empty buildable project reserved for Task 05. `dotnet test` can exit successfully with zero tests; report zero rather than a passing suite.

Test hooks are disabled by default. In a non-Production environment set `TestHooks:Enabled=true` and a non-empty `TestHooks:Token`, then send that token in `X-Test-Token` to `POST /__test/reset`, `/__test/clock` (`instant`, `advanceMinutes`, or `reset` form values), or `/__test/run-jobs`. Disabled or invalid-token requests return 404. Startup fails if hooks are enabled in Production.

## Per-feature checklist

1. Name the full requirement IDs, dependent rules, affected permissions, and observable completion conditions before editing. Translate each condition into a check and expected result.
2. Restore, build, and run relevant tests. Record commands, exit codes, discovered/executed test counts, and failures. After fixing a failure, rerun the check that failed.
3. Start the app and verify the affected flow. For template smoke checks, GET `/` and `/Home/Privacy` should return rendered HTML. Stop any server started for verification afterward.
4. Review `git diff --check`, `git diff`, and `git status --short`, including newly created files (ordinary `git diff` omits untracked content). Separate pre-existing user edits from the task's changes.
5. Review against the FR/BR/NFR, authorization, validation, and security requirements. Update changed setup/convention/decision documents; do not rewrite the SRS without an explicit requirement change.
6. Report evidence and anything unverified. A failure/blocker is not Done. No perpetual retry loop: inspect causes; escalate environmental or unresolved product blockers accurately.

## MVC forms and protected operations

- GET renders the intended view, fields, navigation, and labels.
- Valid POST produces the correct state and redirect; invalid POST returns field/business errors without unintended writes and preserves usable inputs.
- Missing/invalid antiforgery tokens are rejected; GET does not mutate state.
- Anonymous access, every relevant role, wrong owner, and unassigned Staff are checked on the server. Test direct requests, not only hidden navigation.
- Sensitive/extra posted fields cannot change roles, ownership, status, or server-derived price. Return URLs cannot become open redirects.
- Duplicate rules and race conditions are exercised where relevant; persisted state matches the result.
- User-provided text renders encoded; errors expose no secrets/stack traces; desktop layout and keyboard/form usability remain intact.

For registration specifically (FR-AUTH-01/02 and BR-AUTH-01..04/08 as amended by decision 0002): the page renders; valid input creates Active Attendee; username/password boundaries and confirmation errors are visible; case-insensitive duplicate usernames are rejected including concurrent submissions; database hashes verify through Identity `PasswordHasher<TUser>`; success redirects as decided; posted role/status fields are ignored/rejected; build and focused tests pass. Authentication uses cookies; verify idle expiry, logout, next-request role/status changes, and rehash persistence where applicable. The redirect remains to be decided.

For Tailwind styling changes, run the actual CSS build once it exists, verify Razor utility detection and generated asset loading, and check deployment includes compiled CSS. Dependencies are installed through Bun, but no repository CSS build script currently exists; `dotnet build` alone does not establish Tailwind compilation.

## Persistence and timed behavior

Follow [persistence prerequisites/conditional migration workflow](../architecture/persistence.md). Generate and inspect migration/snapshot/SQL, build/test, then apply to an authorized development database when available. Verify applied migration history and actual data/constraints. Test concurrency against PostgreSQL, not only an in-memory substitute.

Check time rules just before, at, and after boundaries: 30-minute idle session, 10-minute hold, 24-hour cancellation, sale windows, timeline touching endpoints, and check-in/event completion. Jobs must run each minute and be idempotent. Seed/reset and controllable-time workflows required by NFR-12/13 are absent; add them when the relevant feature needs them and document safe disposable-database scope.

## Setup verification record (2026-10-03)

The setup change is documentation-only. Verification used SDK 10.0.203:

- `dotnet restore ECMA.sln`: succeeded (exit 0).
- `dotnet build ECMA.sln --no-restore`: succeeded (exit 0), no reported warnings/errors.
- `dotnet test ECMA.sln --no-build`: succeeded (exit 0), but no test projects exist and zero tests executed. This is not a passing feature suite.
- The then-root web project was run with the HTTP launch profile in Development on port 5028. HTTP checks returned 200 for `/`, `/Home/Privacy`, `/css/site.css`, and `/lib/bootstrap/dist/css/bootstrap.min.css`; page contents matched the template. The verification server was stopped afterward.
- The HTTP-only run logged `Failed to determine the https port for redirect.` This is the documented launch-profile limitation, not a build failure. HTTPS/certificate behavior was not tested.
- `git diff --check`: succeeded. Relative links in all 10 harness documents resolved. All task document contents and the pre-existing project diff were reviewed; an independent read-only review found no actionable issues.

No application source, packages, SRS, or project configuration was changed by that task. Its pre-existing web-project edit was preserved. PostgreSQL connection/migrations, authentication, product flows, and automated feature tests were not verified because those components did not exist.
## PostgreSQL integration tests

`tests/ECMA.Domain.Tests/PostgresIntegrationTests.cs` covers races and SQL behavior that mocks cannot prove (last seat, double submit, event cancellation vs registration, search wildcards/NFC/UTC+7 dates, agenda insert). Each test creates and drops its own `ecma_test_*` database on the server named by `ECMA_TEST_CONNECTION_STRING`; without the variable these tests are reported as skipped, not passed.

```text
ECMA_TEST_CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=postgres;Username=ecma;Password=<dev password>" dotnet test ECMA.sln
```

The account needs `CREATEDB`. Use a disposable development server only. Case-insensitive Vietnamese search relies on a UTF-8 database locale (the pinned `postgres` image default); a `C` locale does not fold non-ASCII letters.

## Verification record (2026-10-25): agenda, public search, registration, participants

Scope: task sheet items 7–13 (FR-SCH-01..03, FR-SRCH-01..03, FR-REG-01..04, FR-PART-01..04, plus BR-EVT-04 registration cascade). SDK 10.0.402, PostgreSQL 18.3 (UTF-8 locale) on a local disposable server.

- `dotnet build ECMA.sln`: succeeded. Pre-existing warnings remain (CS0108 in Domain results, a missing `ECMA.Application` reference in the empty `ECMA.Application.Tests` project).
- `dotnet test ECMA.sln` without `ECMA_TEST_CONNECTION_STRING`: 70 passed, 8 skipped. With it: 78 passed, 0 skipped; repeated three times with the same result.
- Migration `AddRegistrations` only creates `registrations` (unique code and `(event_id, user_id)`, FK restrict, status/cancellation/check-in check constraints). The `AgendaItem` key change is model-only (no schema operation). Applied to the local `ecma_dev` database; `migrations list` shows all three migrations.
- Regression: adding an agenda item previously failed with HTTP 500 (`DbUpdateConcurrencyException`, EF issued UPDATE for the new item). `AgendaItem.Id` is now `ValueGeneratedNever`; `AddAgenda_ToDraft_IsInsertedAndPersisted` fails without the fix and passes with it.
- HTTP checks against the running app (HTTPS, real cookies and antiforgery tokens, curl): 118/118 passed. They cover agenda add/overlap/touching boundary/5-minute rule/lock after publish; search keyword (case, diacritics, literal `%`), type and inclusive UTC+7 date filters, invalid/repeated `page`; details visibility (Guest/owner/other organizer/registrant for Draft, Completed, Cancelled), invalid/unknown IDs, encoded agenda content, returnUrl to details; registration order (§4.3) including owner 403, already registered, full, closed after start, MANAGER block; My registrations, self-cancel (403/404/idempotent "Đã hủy"/re-register keeps code); participant list permissions, filters, search by code, stats, Absent only when Completed; manager cancel reason 400, posted `CancelSource` ignored; event cancellation cascading to EVENT while earlier MANAGER cancellation is kept. Start/end boundaries were simulated by shifting event times in the database because no test clock hook exists yet.
- Not verified: visual rendering in Chrome/Edge at 1024 px, and NFR-05 performance.
