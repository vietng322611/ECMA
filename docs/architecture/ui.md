# Server-rendered UI

## Existing conventions

- Views live in `Views/<Controller>/<Action>.cshtml`; shared views/partials live in `Views/Shared`.
- `Views/_ViewStart.cshtml` selects `_Layout`; `_ViewImports.cshtml` imports `ECMA`, `ECMA.Models`, and MVC Tag Helpers.
- `_Layout.cshtml` uses `ViewData["Title"]`, `RenderBody`, and optional `Scripts`. Navigation uses `asp-controller`/`asp-action`; the error page uses `@model ErrorViewModel`.
- Bootstrap 5.3.3, jQuery 3.7.1, jQuery Validation 1.21.0, and unobtrusive validation 4.0.0 are vendored under `wwwroot/lib`. Bun `package.json`/`bun.lock` now declare Tailwind CSS 4.3.3 and its CLI; no Tailwind input/output, build scripts, or layout integration exists yet.
- Shared CSS is `wwwroot/css/site.css`; layout-isolated CSS is `_Layout.cshtml.css`, exposed through `ECMA.styles.css`. Shared JS is `wwwroot/js/site.js`, currently an empty template hook. Asset links use versioning where shown in the layout.
- Current content and `<html lang="en">` are English template defaults, not a settled product language/brand. The SRS allows English or Vietnamese but requires consistency.

## Feature implementation guidance

Use Razor `.cshtml` and strongly typed `*ViewModel` classes. Add `ViewModels/` only when needed and update imports as appropriate. Use Tailwind CSS v4 for new product styling per [decision 0003](../decisions/0003-razor-tailwind-styling.md); retain existing Bootstrap pages until a scoped replacement. No SPA, React, Vue, Angular, Blazor, or separate frontend is implied.

Establish the local Tailwind CLI build only when styling work begins. Compile to `wwwroot`, scan Razor sources, use statically detectable complete class names, and wire output into the layout and deployment process. Document actual commands after creating them; there is no runnable repository CSS script yet. Avoid mixing Bootstrap and Tailwind on migrated pages.

Forms should use MVC Tag Helpers (`asp-for`, labels, validation messages/summary), mark required fields, and show errors near fields. Include `_ValidationScriptsPartial` in the `Scripts` section where client validation is useful. Always validate on the server; rebuild selections/data when redisplaying an invalid form. Use antiforgery-protected POST and redirect after success. Bind only editable fields.

Keep forms usable without JavaScript where practical. JavaScript may enhance confirmations and small interactions; it must not carry authorization or business rules. Dangerous actions need confirmation per SRS §4.2. Show operation success/failure on the page; this is allowed feedback, not the excluded notification feature.

Encode descriptions and timeline text through Razor. Preserve Unicode, keyboard navigation, labels, visible focus, readable contrast, and empty/error states. Check supported desktop browsers at ≥1024 px; mobile design is not required. Role-sensitive navigation is helpful but never replaces server checks.

## Optional design skill

The installed `design-taste-frontend` document calls itself `tasteskill` and is intended for landing pages, portfolios, and redesigns, explicitly not dashboards, tables, or multi-step product UI. No exact `taste-skill` folder was found under the inspected repository, user `.agents/skills`, or `.codex/skills` locations.

Use its audit-first approach and typography/contrast/form-state checks selectively for public-facing pages. Do not adopt its React/Next.js/Motion defaults, extra imagery, automatic dark-mode/mobile scope, or design-system dependencies as project requirements. MVC/Razor, the accepted Tailwind choice, and product requirements take priority. Skills are optional and need not be installed to work on ECMA.