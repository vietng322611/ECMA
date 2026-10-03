# ECMA — Events and Conferences Management Application

ECMA is a desktop-browser web application for organizing events and conferences, registering attendees, managing tickets, and tracking attendance. Inspired by Eventbrite, it is developed as a **Software Verification & Validation course project**.

The goal is a minimal, usable event-management system with clear business rules that support requirement-based testing: boundary values, equivalence partitioning, state transitions, and decision tables.

## Intended capabilities

| User | Planned capabilities |
|---|---|
| Guest | Browse and search public events; view event details, timelines, and ticket types. |
| Attendee | Register and sign in, manage a profile, order tickets, complete mock payments, view tickets and a personal schedule, and cancel eligible registrations. |
| Organizer | Create and publish events, manage timelines and ticket inventory, manage participants, assign check-in staff, and view event reports. |
| Staff | Check in attendees and undo check-ins for assigned events. |
| Admin | Manage users, roles, and account status; oversee events and system reporting. |

Conferences use the same workflow as events: a conference is an event tagged **Conference**, not a separate event type.

## Scope and conventions

- Payments and refunds are simulated; no real payment gateway is used.
- Accounts are identified by username, not email.
- Email and notifications are excluded. Users view statuses and operation feedback in the application.
- Access is role-scoped, with event ownership and staff assignments enforced on the server.
- Times are stored in UTC and displayed in **Asia/Ho_Chi_Minh (UTC+7)**.
- Monetary amounts are integer **VND** values.
- The target is current desktop Chrome, Firefox, and Edge, with a minimum viewport width of 1024 px.

## Technology and current status

**The repository currently contains an ASP.NET Core MVC template, not an implemented event-management system.** Home, Privacy, and Error pages exist; domain features, persistence, authentication, and automated tests do not yet exist.

| Area | Technology / status |
|---|---|
| Runtime | .NET 10 and ASP.NET Core MVC |
| Rendering | Server-rendered Razor (`.cshtml`); no separate SPA is required |
| Database | PostgreSQL with EF Core — planned, not configured |
| Authentication | Cookies and Identity `PasswordHasher<TUser>` — accepted, not implemented |
| Styling | Tailwind CSS v4 dependencies installed through Bun; no CSS build pipeline yet, and the template still uses Bootstrap |
| Testing | No test projects yet |

Accepted decisions take precedence over conflicting SRS wording: the application uses MVC/Razor rather than a separate frontend/REST API, and Identity PBKDF2 password hashing rather than bcrypt/argon2.

## Run locally

Prerequisite: a **.NET 10 SDK**. Run these commands from the repository root:

```shell
dotnet restore ECMA.sln
dotnet build ECMA.sln --no-restore
dotnet run --project ECMA.csproj --launch-profile http --no-build
```

Open **http://localhost:5028**. This currently serves the template pages only; no PostgreSQL connection is needed for the template.

The HTTP-only profile may log `Failed to determine the https port for redirect.` For HTTPS development, trust the development certificate and use the HTTPS profile:

```shell
dotnet dev-certs https --trust
dotnet run --project ECMA.csproj --launch-profile https --no-build
```

The HTTPS address is **https://localhost:7076**.

For frontend dependency work, install [Bun](https://bun.sh/) and run `bun install`. This installs the existing Tailwind dependencies; it does not compile CSS or replace Bootstrap.

## Verification

```shell
dotnet test ECMA.sln --no-build
```

There are currently **zero test projects**. A successful command with no tests is not evidence that product features work. See the [verification guide](docs/development/verification.md) for runtime checks and feature acceptance criteria.

## Repository guide

- `Program.cs` — application startup and MVC routing.
- `Controllers/`, `Models/`, `Views/` — current MVC template.
- `wwwroot/` — static assets.
- `docs/product/` — requirements, feature dependencies, and open questions.
- `docs/decisions/` — accepted technology and architecture decisions.
- `docs/architecture/` — architecture, persistence, and UI guidance.
- `docs/development/` — conventions and verification workflow.

## Documentation

- [Software Requirements Specification](docs/product/SRS_ECMA.md) — detailed functional requirements, business rules, and non-functional requirements.
- [Product context](docs/product/agent-context.md) — feature map and unresolved decisions.
- [Architecture overview](docs/architecture/overview.md).
- [Accepted decisions](docs/decisions/README.md).
- [Development conventions](docs/development/conventions.md).
- [Verification guide](docs/development/verification.md).