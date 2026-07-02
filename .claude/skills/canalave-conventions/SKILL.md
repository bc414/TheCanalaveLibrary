---
name: canalave-conventions
description: >
  Blazor/.NET/EF Core coding conventions for The Canalave Library.
  Use when writing, reviewing, or planning code in this project.
  Covers EF Core (TPT, Npgsql, migrations), Blazor (InteractiveAuto,
  component design, [PersistentState]), CQRS-lite (services, DTOs),
  Redis, Aspire, naming, code organization, automated testing
  (Unit / Integration / RazorComponents-bUnit tiers, Testcontainers-Postgres,
  fake IActiveUserContext), and full-breadth debugging methodology (repro
  discipline, diagnostic-surface correlation, browser-based debugging).
paths: "*.cs, *.razor, *.csproj"
---

# The Canalave Library — Coding Conventions

Authoritative paradigm-correctness reference. Conventions are derived from the design spec's
architectural decisions, validated against current framework docs. **The codebase is the subject of
review, not the source of truth** — where existing code disagrees with these conventions, the code is
what's wrong.

## Target Platform

- **.NET 10** (all projects share the same major version). C# 14.
- **EF Core 10** + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 + `EFCore.NamingConventions` 10.
- **.NET Aspire 13** for local orchestration (dev only — never deployed).
- **Blazor Web App**, global `InteractiveAuto`.
- **PostgreSQL** (primary + read replica), **Redis** (write-behind/cache), **Cloudflare R2 / MinIO** (blobs).

## Settled Architectural Axioms

These have documented rationale and rejected alternatives. **Do not propose alternatives:**

1. **PostgreSQL over SQL Server** — free read replicas, MVCC, JSONB, tsvector/GIN FTS.
2. **Table-per-Type (TPT) over TPH** — explicit `.ToTable()` per type; NOT NULL child FKs.
3. **Boolean interaction columns over enum/junction tables** — `UserStoryInteraction` uses `bool` columns with `Has-`/`Is-` prefix convention.
4. **`int` user IDs over GUIDs** — `User : IdentityUser<int>`, `ApplicationRole : IdentityRole<int>`.
5. **snake_case via `EFCore.NamingConventions`** — never hand-name tables/columns.
6. **CQRS-lite with a DTO firewall** — UI never sees EF Core model classes.
7. **Write-behind Redis queue** — high-frequency writes go to Redis, drained by background worker.
8. **Global `InteractiveAuto` (end state)** — SSR prerender → SPA via WASM. Set render mode on `<Routes>`/`<HeadOutlet>` in `App.razor` (never on `RouteView`); `InteractiveServer` is the spec-sanctioned dev shortcut until WASM ships. See `cross-cutting.md` §"Render Mode".
9. **Tailwind CSS v4** — utility-first, no component library. Design tokens in an `@theme` block
   (CSS-first config; see `layer4-style.md`), not `tailwind.config.js`.
10. **Three-axis search** — Source × Filter × Sort. FTS is a filter, not a source.

## Project Boundaries (enforced by references)

| Project | Role | Hard rule |
|---|---|---|
| **Core** | POCOs, service interfaces, enums, DTOs, constants | NO EF Core packages except `Microsoft.EntityFrameworkCore.Abstractions`. |
| **SharedUI** | All `.razor` components/pages, layouts, `_Imports.razor` | NO WASM-specific NuGet. References Core only. |
| **Server** | `App.razor`, both DbContexts, server service impls, workers, API endpoints, Identity pages | Identity components MUST stay here. |
| **Client** | Client `Program.cs`, HTTP-based service impls | Injects `HttpClient`, calls API endpoints. |
| **AppHost** | Aspire orchestrator | Dev only, never deployed. |

## Code Organization

**Vertical (folder-per-feature) is the hard rule.** Group by feature per `folder_clusters.md`, never by technical layer. One flat namespace per project: `TheCanalaveLibrary.Core`, `.Server`, `.Client`, `.SharedUI` — a file that moves folders never changes its namespace. A cross-cutting cluster is legitimate when its *feature is a shared concern* that no single consuming feature owns.

Cross-cutting clusters and their scope:
- **`Lookups/`** — seeded reference data (themes, report reasons, etc.) every cluster queries by FK; no single owning feature.
- **`RichText/`** — `RichTextView`/`EditorView` universal render/edit atoms consumed by Chapters, Comments, Recommendations, BlogPosts, Profiles, Messaging.
- **`Dialogs/`** — `ConfirmDialog` universal confirm/cancel modal (spoiler reveal, account deletion, leaving a group, etc.); no owning feature.
- **`Users/`** — `UserCardDto`/`UserCard` universal user-summary atom consumed across Following, Profiles, Groups, Comments, Recommendations, Messaging, tree-search. Distinct from `Identity/` (which owns the `User` entity).
- **`Images/`** — `IImageStorageService` universal user-upload-blob write path (story covers, profile pictures). Distinct from `Sprites/` (read-only git-managed static assets).
- **`Identity/`** — `User` entity + `IActiveUserContext` + auth plumbing (Server impl: `Server/Identity/`). Distinct from `Users/` (summary atom) and `Profiles/` (read/edit layer).
- **`Sprites/`** — `IThemeReadService`/`ISpriteReadService` render-time URL resolution (read-only). Distinct from `Images/` (upload) and `Identity/` (the entities projected).
- **`Discovery/`** — `StoryFilterDto`, the three §8.7 filter-setting entities, and `ResultsFilterPanel`/`TagFilter`/`UserStoryInteractionFilter` coordination components; consumed by search, profiles, and bookshelves.
- **`Bookshelves/`** — `BookshelfTab` enum + `BookshelfTabSlug` slug helper consumed by `SharedUI/Bookshelves/` and `Server/UserStoryInteractions/`.
- **`Recommendations/`** — SVG icon constants and display components spanning submission, display, Hidden Gem, and attribution sub-features.
- **`Messaging/`** — Messaging feature cluster; `EditorView` and `UserCard` remain in their own cross-cutting clusters.
- **`Profiles/`** — projection and settings-edit services *over* the `User` entity. Boundary: Identity = entity + auth plumbing; Profiles = how the entity is read and edited by owner or public viewer.

API endpoint classes (`{Feature}Endpoints.cs`, `Map{Feature}Endpoints()`) colocate in the feature
cluster folder next to the server service impl they wrap (e.g. `Server/Sprites/SpriteEndpoints.cs`
beside `Server/Sprites/ServerSpriteReadService.cs`), **not** flattened into one `Endpoints/` folder.
This is deliberately the opposite of EF configuration's rule below: a route is normally edited in
lockstep with the service method it calls (same edit-locality argument that places service impls in
cluster folders), whereas EF configuration is a cross-feature delete-cascade *graph* edited as a whole
at migration time. `Server/Endpoints/` and `Server/Data/Configurations/` look structurally similar
(flat, one file per feature) but exist for opposite reasons — don't generalize one into the other.

**Legacy technical-layer folders are deprecated:** `Core/Models/` (entities), `Core/ServiceInterfaces/`
(service interfaces), `Server/Services/` and `Client/Services/` (service impls), `Server/Endpoints/`
(route mapping). These pre-date the vertical convention and aggregate many unrelated features by
technical kind instead of by feature. No new file is ever added to one. Any work-unit that touches a
file still living in one of them **moves it into its feature cluster as part of that work-unit** — not
optional polish, part of finishing the work. Do not pre-create empty cluster-folder skeletons ahead of
need (SDK-style projects drop empty folders from the build anyway, and placeholder files invite drift);
clusters appear just-in-time, one work-unit at a time, as files migrate into them. If a work-unit's plan
leaves a touched file behind in a legacy folder, that's a plan defect — fix the plan, not just the code.

### Enforcing the Flat Namespace on Razor Files

Razor's default namespace is folder-path-based (`RootNamespace + folder path`), which silently
diverges from the flat-namespace policy the instant a `.razor` file moves folders — there is no
compiler error until some unrelated file's reference happens to break, if ever.

- **Every `.razor` file MUST start with an explicit `@namespace` directive** matching its project
  (`@namespace TheCanalaveLibrary.Server`, `.SharedUI`, or `.Client`) — first line, or second line
  right after `@page` if the file has one. `_Imports.razor` files are the only exception (they hold
  `@using` directives, not a namespace).
- **When moving or renaming a folder that contains `.razor` files**, grep the whole repo for the old
  dotted-path namespace string (e.g. `TheCanalaveLibrary.Server.OldFolderName`) to catch stale
  `@using` directives and fully-qualified `typeof(...)` references. This is exactly how namespace
  drift went unnoticed after a past `Components` → `Identity` folder rename.
- **`_Imports.razor` cascades to subfolders only, never to siblings.** Each top-level folder branch
  (e.g. `Identity/Pages`, `Identity/Pages/Manage`, `Identity/Shared`) needs its own `_Imports.razor`
  for cross-project usings (`TheCanalaveLibrary.Core`, `TheCanalaveLibrary.SharedUI`), even though
  every component in the project ultimately shares one flat namespace.
- **Co-located component assets** (`Component.razor.js`, `Component.razor.css`) are referenced via
  `@Assets["PhysicalFolderPath/Component.razor.js"]` — the *physical* folder path, not the
  namespace. Folder renames break these silently (404 at runtime, no compile error).

## Layer Files — Read Before Working

Each file covers one layer of the 8-layer architecture plus cross-cutting concerns. Read the relevant
file(s) before writing code. **Writing or touching any `class="..."` attribute counts as "writing
code" for `layer4-style.md`, even on a one-line wrapper `<div>` for "functional only, no styling"
work** — see that file's "Bootstrap debris warning." Skipping it because the change feels small is
exactly how Bootstrap-template classnames (`top-row`, `nav-pills`, …) get copied into new markup.

| File | Layer | Scope |
|---|---|---|
| [layer1-data-model.md](layer1-data-model.md) | 1 | EF Core entities, Fluent API, TPT, enums, migrations, vertical partitioning |
| [layer2-services.md](layer2-services.md) | 2 | Service interfaces, CQRS split, DTOs, DbContext injection, service composition |
| [layer3-logic.md](layer3-logic.md) | 3 | `@code` blocks: parameters, services, events, state, `[PersistentState]`, `EditForm`, debounce, optimistic updates, component tier × logic |
| [layer3.5-structure.md](layer3.5-structure.md) | 3.5 | Markup skeleton: component composition, HTML elements, `@if`/`@foreach`, `@ChildContent`, data flow through `[Parameter]`, `<AuthorizeView>`, dispatcher pattern, desktop/mobile branching |
| [layer4-style.md](layer4-style.md) | 4 | Tailwind utility classes, sprite resolution, responsive variants, outer margin rule, parameter-based variants, conditional class expressions |
| [layer5-wasm.md](layer5-wasm.md) | 5 | API endpoints, client services, `PersistentAuthenticationStateProvider` |
| [layer6-indexes.md](layer6-indexes.md) | 6 | Filtered, composite, golden, GIN indexes — pure DDL |
| [layer7-redis.md](layer7-redis.md) | 7 | Write-behind buffer, ephemeral store, read-side cache |
| [layer8-data-marts.md](layer8-data-marts.md) | 8 | Non-EF background workers, raw SQL, table swap |
| [cross-cutting.md](cross-cutting.md) | All | Render mode, device detection, Identity, Aspire, notifications, badges, UserStats, content rating filtering, delete policies |
| [testing.md](testing.md) | All | Three test tiers by *kind* (Unit = directly-constructed, no host/DB; Integration = `WebApplicationFactory`/Testcontainers Postgres; RazorComponents = bUnit render tests); Testcontainers-Postgres rule; fake `IActiveUserContext`; what dev-diagnostics endpoints are/aren't for |
| [debugging.md](debugging.md) | All | Full-breadth debugging methodology: reproduce before fixing, correlating diagnostic surfaces (server log / exception page / browser console+network / psql / dev-diagnostics), re-running the identical repro after a candidate fix (a moved stack trace ≠ fixed), feature-local vs. cross-cutting scope classification, when to reach for browser-based debugging, fix-same-session discipline |

## Component Taxonomy Quick Reference

| Tier | Logic (L3) | Structure (L3.5) | Style (L4) | Service Injection |
|---|---|---|---|---|
| **Leaf** | Thin: params, EventCallbacks, computed display props | Full: HTML elements, `@if`/`@foreach` | Full: all visual identity | Never |
| **Composite** | Varies by subtype (coordination = heavy) | Main job: compose children | Light: layout Tailwind, container framing | Rarely (independent concerns only) |
| **Page/Dispatcher** | Heavy: service injection, data loading, device detection | Thin: `@if (mobile)` branch | Near zero: loading skeleton | Always |

**Composite subtypes:** pass-through layout, coordination, container, third-party wrapper.

## Naming Quick Reference

- **Model classes:** singular PascalCase (`UserStoryInteraction`). **DbSets:** plural.
- **Enums:** suffix `...Enum` for hybrids (`StoryStatusEnum`); `: short`, 0-indexed, stored as `smallint`.
- **Reading status booleans:** `Has-` prefix for permanent past events (`HasStarted`), `Is-` prefix for current mutable state (`IsCompleted`, `IsIgnored`).
- **Service interfaces (read):** `I{Feature}ReadService`. **(write):** `I{Feature}WriteService`.
- **Server impls:** `Server{Feature}ReadService`. **Client impls:** `Client{Feature}ReadService`.
- **DTOs:** `{Entity}{Purpose}Dto`. **ViewModels:** `{Feature}ViewModel`.
- **EF configs:** `{Entity}Configuration : IEntityTypeConfiguration<T>`, colocated (all clusters) in
  `TheCanalaveLibrary.Server/Data/Configurations/`, grouped one file per folder-cluster — see
  [layer1-data-model.md](layer1-data-model.md) §"Fluent API Organization".
- **API endpoints:** `{Feature}Endpoints.Map{Feature}Endpoints(this WebApplication app)`.
- **Indexes:** `ix_{table}_{columns}` via `HasDatabaseName()`.
- **User model:** `User` (NOT `ApplicationUser`). **Role:** `ApplicationRole`.
- **Namespaces:** One per project (`TheCanalaveLibrary.Core`, `.Server`, `.Client`, `.SharedUI`).
- **Container components:** `StoryDeck` (NOT `StoryList`). `TagChip` (NOT pill/token/badge).
- **Interaction button:** `UserStoryInteractionButton` (verbose — deliberate).
- **`UserStoryInteraction` prefix rule:** Every identifier meaning *user×story interaction* must be
  spelled `UserStoryInteraction…` — never the bare prefix `Interaction…`. "Interaction" is too generic;
  `UserStoryInteraction` is the site's primary feature. This applies to types, enums, constants, params,
  method names, and file names. **Deliberately NOT renamed** (different domain or prose):
  - `UserChapterInteraction`/`LastInteractionDate` — chapter-reading domain, already fully qualified
    (User + Chapter + Interaction); leave unchanged.
  - Bare `Interaction`/`Interactions` inside prose comments and seed-data description strings — not identifiers.

## Key Domain Terms

| Term | Meaning | NOT |
|---|---|---|
| Recommendation | Substantive written endorsement | "Review" |
| Followed Users | User-to-user relationship | "Followed Authors" (not everyone is an author) |
| Bookshelves | Personal reading management | A discovery surface |
| HasStarted | Permanent past event (reading began) | Current state indicator |
| StoryDeck | Container holding StoryCards | StoryList, StoryCatalog |
| Tag Directory | User-facing tag browse + mod edit | TagLibrary (mod-only, rejected) |
