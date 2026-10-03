# Decision: Razor Views with Tailwind CSS

Status: Accepted

## Context

The user explicitly selected Razor `.cshtml` for the frontend and Tailwind CSS for styling on 2026-10-03. The existing MVC template uses Bootstrap. The user has installed Tailwind CSS 4.3.3 and `@tailwindcss/cli` through Bun; `package.json` and `bun.lock` exist, but no Tailwind input/output, script, or layout integration exists yet.

## Decision

Keep server-rendered Razor Views and MVC Tag Helpers. Use locally compiled Tailwind CSS v4 for new product UI. Preserve existing Bootstrap template pages until a scoped styling change replaces them; do not silently redesign the application or combine both frameworks on migrated pages.

## Consequences

When styling work begins, establish a minimal CLI build that scans Razor source and emits CSS under `wwwroot`. Document the actual paths and commands, include compilation in deployment/publish preparation, and serve the generated CSS through the Razor layout. Use complete literal utility classes or explicitly registered sources for classes that cannot be detected statically. Do not use a production CDN runtime.

Bun tooling is only an asset-build dependency, not a separate frontend application. This choice does not introduce React, Next.js, a SPA, Tailwind v3 configuration conventions, or additional design libraries. Preserve labels, validation messages, keyboard accessibility, and server-side validation. The existing Dockerfile does not compile Tailwind assets; address deployment integration when the asset pipeline is implemented.

No UI redesign or asset pipeline is implemented by this documentation change.