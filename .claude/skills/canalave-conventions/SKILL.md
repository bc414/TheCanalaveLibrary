---
name: canalave-conventions
description: >
  Blazor/.NET/EF Core coding conventions for The Canalave Library.
  Use when writing, reviewing, or planning code in this project.
  Covers EF Core (TPT, Npgsql, migrations), Blazor (InteractiveAuto,
  component design, [PersistentState]), CQRS-lite (services, DTOs),
  Redis, Aspire, naming, and code organization.
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
8. **Global `InteractiveAuto`** — SSR prerender → SPA via WASM. Set on `<RouteView>` in `Routes.razor`.
9. **Tailwind CSS** — utility-first, no component library. Design tokens in `tailwind.config.js`.
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

**Vertical (folder-per-feature) is the target.** Group by feature (`Stories/`, `Tags/`, `UserStoryInteractions/`),
not by technical layer. One flat namespace per project regardless of folder depth:
`TheCanalaveLibrary.Core`, `.Server`, `.Client`, `.SharedUI`.

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
file(s) before writing code.

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
- **EF configs:** `{Entity}Configuration : IEntityTypeConfiguration<T>`.
- **API endpoints:** `{Feature}Endpoints.Map{Feature}Endpoints(this WebApplication app)`.
- **Indexes:** `ix_{table}_{columns}` via `HasDatabaseName()`.
- **User model:** `User` (NOT `ApplicationUser`). **Role:** `ApplicationRole`.
- **Namespaces:** One per project (`TheCanalaveLibrary.Core`, `.Server`, `.Client`, `.SharedUI`).
- **Container components:** `StoryDeck` (NOT `StoryList`). `TagChip` (NOT pill/token/badge).
- **Interaction button:** `UserStoryInteractionButton` (verbose — deliberate).

## Key Domain Terms

| Term | Meaning | NOT |
|---|---|---|
| Recommendation | Substantive written endorsement | "Review" |
| Followed Users | User-to-user relationship | "Followed Authors" (not everyone is an author) |
| Bookshelves | Personal reading management | A discovery surface |
| HasStarted | Permanent past event (reading began) | Current state indicator |
| StoryDeck | Container holding StoryCards | StoryList, StoryCatalog |
| Tag Directory | User-facing tag browse + mod edit | TagLibrary (mod-only, rejected) |
