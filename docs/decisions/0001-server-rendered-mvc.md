# Decision: Server-rendered MVC

Status: Accepted

## Context

The repository-setup task explicitly requires ASP.NET Core MVC, Razor server rendering, .NET 10, PostgreSQL, and EF Core, without a separate frontend/backend application. Existing code is a single .NET 10 MVC template. SRS §2.1 shows a Frontend/REST API architecture, while §4.3 labels API endpoints as suggestions for testing.

Approval source: the user's explicit repository-setup brief, 2026-10-03. This record captures that instruction, not a new agent-selected architecture.

## Decision

Use one ASP.NET Core MVC application with server-rendered Razor Views. Add EF Core/PostgreSQL persistence when feature work requires it. Use Controllers and services for HTTP/business boundaries without speculative architecture layers. Do not add a SPA or internal API solely to serve Razor pages.

## Consequences

The SRS remains authoritative for product behavior, roles, security, and data rules, but its frontend/API diagram does not require separate applications for this project. Enforce server-side permissions/validation for MVC requests as well as any explicitly requested API endpoints. JSON endpoints may be added only for a concrete requirement; confirm whether course API testing is required before treating the suggested endpoint list as a deliverable.

No domain code, database configuration, or authentication was implemented by this decision. A later architecture change needs an explicit task or a replacement accepted decision and corresponding verification.