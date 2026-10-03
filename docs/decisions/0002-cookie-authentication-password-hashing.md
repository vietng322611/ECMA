# Decision: Cookie authentication and Identity password hashing

Status: Accepted

## Context

The user explicitly selected cookie authentication and database-stored password hashes using `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` on 2026-10-03. Authentication and persistence are not implemented yet. BR-AUTH-08 names bcrypt/argon2, which conflicts with this choice: Identity's default password hasher uses PBKDF2.

## Decision

Use ASP.NET Core cookie authentication for the server-rendered MVC application. Hash passwords with `PasswordHasher<TUser>` and store only the resulting hash in the PostgreSQL user record. Verify through `VerifyHashedPassword`; handle `SuccessRehashNeeded` by persisting an upgraded hash after successful verification. Never implement password hashing manually or store plaintext passwords.

The explicit user decision overrides BR-AUTH-08's algorithm choice with Identity password hashing; the prohibition on plaintext storage remains unchanged. Keep the original SRS wording for traceability. This does not require adopting the full Identity account/store/UI framework, JWTs, or bearer-token authentication.

## Consequences

Future AUTH implementation must configure cookie authentication and place `UseAuthentication` before `UseAuthorization`. Enforce the SRS's 30-minute inactivity timeout, password rules, case-insensitive username uniqueness/login, and server-side permissions. Cookie sliding expiration alone does not establish an exact idle-timeout guarantee; verify the chosen activity semantics at boundaries.

Revalidate account status and role against authoritative state on each request so disabling accounts and changing roles take effect as required. Protect authentication cookies with HttpOnly, appropriate SameSite, and Secure in deployed HTTPS environments; preserve antiforgery protection and local-only return URLs. Test login/logout, hash verification/rehashing, invalid credentials, idle expiry, and existing-session invalidation. Redirect targets and Admin seed credential provisioning remain unresolved.

This decision records future implementation requirements; no cookie scheme, user entity, database, or login flow is added by this documentation change.