# Verification and feature completion

Run commands from the repository root with a .NET 10 SDK. `ECMA.sln` currently contains one web project and no test projects. No CI, test runner packages, seed/reset scripts, local tool manifest, or PostgreSQL environment is configured. Keep this document current as infrastructure is introduced.

## Supported baseline commands

```text
dotnet --info
dotnet restore ECMA.sln
dotnet build ECMA.sln --no-restore
dotnet test ECMA.sln --no-build
dotnet run --project ECMA.csproj --launch-profile http --no-build
```

The HTTP profile serves `http://localhost:5028`; the HTTPS profile uses `https://localhost:7076` and HTTP 5028. HTTP-only development may log a missing HTTPS-port warning because HTTPS redirection is configured; use the HTTPS profile with a trusted development certificate when testing HTTPS. Do not suppress production HTTPS protections.

`dotnet test` may exit successfully with no tests: report zero tests rather than a passing suite. When a feature needs automated tests, add the smallest suitable test project/runner, include it in the solution, and document its exact commands. If placed under this root web project's directory, exclude test sources/content from the web project's SDK globs and verify the build boundary. No runner is selected by this setup.

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
- `dotnet run --project ECMA.csproj --launch-profile http --no-build`: started in Development on port 5028. HTTP checks returned 200 for `/`, `/Home/Privacy`, `/css/site.css`, and `/lib/bootstrap/dist/css/bootstrap.min.css`; page contents matched the template. The verification server was stopped afterward.
- The HTTP-only run logged `Failed to determine the https port for redirect.` This is the documented launch-profile limitation, not a build failure. HTTPS/certificate behavior was not tested.
- `git diff --check`: succeeded. Relative links in all 10 harness documents resolved. All task document contents and the pre-existing project diff were reviewed; an independent read-only review found no actionable issues.

No application source, packages, SRS, or project configuration was changed by this task. The pre-existing `ECMA.csproj` edit was preserved. PostgreSQL connection/migrations, authentication, product flows, and automated feature tests were not verified because those components do not exist.