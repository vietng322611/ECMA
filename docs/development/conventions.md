# Development conventions

## Observed

- One MVC web project and solution: `ECMA.csproj`, `ECMA.sln`; root namespace `ECMA` with file-scoped `ECMA.Controllers` and `ECMA.Models`.
- C# classes/properties/actions use PascalCase; controller classes end in `Controller`, view-specific model uses `ViewModel`. Nullable reference types and implicit usings are enabled.
- Four-space indentation in current C#; static assets/layout formatting varies. Match the touched file; no `.editorconfig` is present.
- DI and middleware are configured in top-level `Program.cs`. `HomeController` has only view/error actions; there is no constructor-injection/service pattern yet.
- Conventional routing and Razor Tag Helpers; `ErrorViewModel.RequestId` is nullable, errors disable caching and expose a request ID, not a stack trace.
- Logging configuration uses built-in providers/levels; no custom logger or operational-log implementation exists.
- No entities, repositories, ViewModels folder, forms/server-validation convention, authentication, migration workflow, tests, README, CI, NuGet overrides, or local .NET tool manifest exists. Bun `package.json`/`bun.lock` now declare Tailwind CSS and its CLI as development dependencies; no CSS build scripts exist yet.
- `AGENT.md` is a compatibility pointer; `AGENTS.md` contains canonical instructions. Existing empty `agent/`, `tasks/`, and `docs/architechture/` do not establish an orchestration or architecture framework. Do not rewrite user-added project folder entries solely to fix that spelling.

## Recommendations for new code (not existing implementation)

- Preserve the single MVC project. Use `Controllers/`, `Models/`, `Views/`, and `wwwroot/`; introduce `ViewModels/`, `Services/`, or `Data/` only for files a feature needs.
- Name presentation models `*ViewModel`, business services `*Service`, and asynchronous service/database methods `*Async`. Preserve action/view routing names when choosing async action names.
- Prefer constructor injection. DbContext normally has scoped lifetime; background jobs must create scopes rather than capture scoped services in singletons. Avoid interfaces with one implementation unless a real testing/integration boundary warrants them.
- Validate trust-boundary input with DataAnnotations/`ModelState` and enforce business invariants in services and database constraints. Bind explicit allowed inputs; derive roles, ownership, prices, and account status on the server.
- Authentication uses cookies and database-stored `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` hashes per [decision 0002](../decisions/0002-cookie-authentication-password-hashing.md). This explicitly replaces BR-AUTH-08's bcrypt/argon2 algorithm choice, not its plaintext prohibition. Full Identity stores/UI are not required by the hasher choice.
- New product UI uses Razor `.cshtml` with locally compiled Tailwind CSS v4 per [decision 0003](../decisions/0003-razor-tailwind-styling.md). Existing Bootstrap is template legacy, not the styling direction for new features.
- Log important actions/errors through `ILogger<T>` without passwords, session tokens, or connection secrets. Handle expected validation/conflicts explicitly; let unexpected exceptions reach the safe handler rather than swallowing them.
- Use a testable time source (built-in `TimeProvider` is a candidate) for time-dependent features; do not add a custom clock framework without need. Tests should cite full FR/BR IDs and cover significant boundaries/authorization/races.
- For significant choices or ambiguity resolutions, use [decision records](../decisions/README.md). Do not create records for ordinary naming or trivial implementation details.

## Skills inspected

User-level `.agents/skills` contains `ponytail`, `caveman`, and `design-taste-frontend`; Codex's inspected `.codex/skills` contains system skills, not those exact three names. Discoverability depends on the agent environment; do not require workstation-specific paths or assume all future agents load them.

`ponytail` favors inspecting/reusing existing code, native/standard-library solutions, and minimal changes; it explicitly preserves validation, security, accessibility, and runnable checks. `caveman` governs concise communication and explicitly exempts persisted docs from compressed grammar. Selective design guidance is in [UI](../architecture/ui.md). These conclusions are incorporated into `AGENTS.md`; no copied skill payload or installation is required.