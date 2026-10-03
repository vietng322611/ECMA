# Decision records

Create a small numbered Markdown record only for significant choices: authentication/session/hash mechanism, authorization model, major schema/architecture changes, long-term dependency choices, or resolution of ambiguous requirements. Do not document every implementation detail as an ADR.

Read accepted records before implementation. Proposed records do not override the SRS. If an explicit task conflicts with an accepted record, identify the conflict and update/supersede the record after the new direction is confirmed. Include relevant FR/BR/NFR IDs and the approval source; never label an agent's unapproved assumption Accepted.

Use `NNNN-short-title.md` and this format:

```markdown
# Decision: <title>

Status: Accepted | Proposed | Superseded

## Context

Requirement IDs, conflict/options, and approval source.

## Decision

The chosen behavior or boundary.

## Consequences

Tradeoffs, affected components, verification, and migration/rollback needs where relevant.
```

Preserve superseded records and link replacements rather than rewriting history. Update architecture/convention documentation when a decision changes it.

## Index

- [0001: Server-rendered MVC](0001-server-rendered-mvc.md) — Accepted from the explicit repository-setup brief.
- [0002: Cookie authentication and Identity password hashing](0002-cookie-authentication-password-hashing.md) — Accepted from the explicit user follow-up; overrides the SRS algorithm wording.
- [0003: Razor Views with Tailwind CSS](0003-razor-tailwind-styling.md) — Accepted from the explicit user follow-up; template styling migration remains future work.