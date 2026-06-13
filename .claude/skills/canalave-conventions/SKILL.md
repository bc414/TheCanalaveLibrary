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
what's wrong (it carries patterns from an earlier AI assistant that may be outdated).

## Target Platform

- **.NET 10** (all projects share the same major version). C# 14.
- **EF Core 10** + `Npgsql.EntityFrameworkCore.PostgreSQL` 10 + `EFCore.NamingConventions` 10.
- **.NET Aspire 9.5** for local orchestration (dev only — never deployed).
- **Blazor Web App**, global `InteractiveAuto`.
- **PostgreSQL** (primary + read replica), **Redis** (write-behind/cache), **Cloudflare R2 / MinIO** (blobs).

## Settled Architectural Axioms

These have documented rationale and rejected alternatives in spec §1–3 and §6. **Do not propose
alternatives to these** — encode them, build on them.

1. **PostgreSQL over SQL Server** — free read replicas (streaming replication), MVCC, JSONB, tsvector/GIN FTS.
2. **Table-per-Type (TPT) over TPH** — explicit `.ToTable()` per type, despite EF Core's TPH default. TPT
   gives NOT NULL child FKs and natural hot/cold vertical partitions.
3. **Boolean interaction columns over enum/junction tables** — `UserStoryInteraction` uses `bool` columns
   (`IsFavorited`, `IsFollowed`, `IsIgnored`), not an enum-keyed junction table.
4. **`int` user IDs over GUIDs** — `User : IdentityUser<int>`, `ApplicationRole : IdentityRole<int>`.
   Smaller composite keys, better index performance on high-traffic junction tables.
5. **snake_case via `EFCore.NamingConventions`** — C# is PascalCase; the plugin auto-converts to
   snake_case. Never hand-name tables/columns.
6. **CQRS-lite with a DTO firewall** — UI never sees EF Core model classes. Reads return DTOs via
   `.Select()` projections on a `NoTracking` read context; writes use tracked entities on the write context.
7. **Write-behind Redis queue** — high-frequency interaction writes go to a Redis list, drained by a
   background worker that batch-writes to PostgreSQL. API controllers are "fast and dumb."
8. **Global `InteractiveAuto`** — SSR prerender first request, then SPA via WASM. Single render mode set on
   `<Routes>`/`<HeadOutlet>` in `App.razor`, not per-component.

## Project Boundaries (enforced by references)

| Project | Role | Hard rule |
|---|---|---|
| **Core** | POCOs, service interfaces, enums, DTOs, constants | NO EF Core packages except `Microsoft.EntityFrameworkCore.Abstractions`. Shared by server + WASM. |
| **SharedUI** | All `.razor` components/pages, layouts, `_Imports.razor` | NO WASM-specific NuGet. Render-mode directives are metadata only. References Core only. |
| **TheCanalaveLibrary.Server** (server) | `App.razor`, both DbContexts, server service impls, workers, API controllers, Identity components | Identity components MUST stay here (`UserManager`/`SignInManager`/`HttpContext`). |
| **Client** (WASM) | Client `Program.cs`, HTTP-based service impls | Service impls inject `HttpClient`, call API endpoints. |
| **AppHost** | Aspire orchestrator | Dev only, never deployed. |

## Code Organization

**Vertical (folder-per-feature) is the target.** Group by feature (`Stories/`, `Tags/`, `Interactions/`),
not by technical layer (`Services/`, `Interfaces/`, `Dtos/`). The codebase is mid-transition from
horizontal — when you touch a feature, move its pieces toward a feature folder rather than adding to the
old layer folders. Interface + both impls + DTOs for one feature live together (interface in Core,
impls in their respective projects).

## Detailed references

Read the relevant file before writing code in that area — depth lives in these, SKILL.md stays a hub.

- EF Core config, TPT, Npgsql, migrations, enum/lookup framework, vertical partitioning → [efcore-patterns.md](efcore-patterns.md)
- Blazor render modes, `[PersistentState]`, component isolation, the prerender→interactive transition → [blazor-patterns.md](blazor-patterns.md)
- CQRS-lite, DTO firewall, dual service implementations, DTO vs primitive vs ValueTuple → [service-patterns.md](service-patterns.md)
- Redis write-behind, background workers, Aspire wiring, sprites/blobs, naming tables → [infrastructure.md](infrastructure.md)

## Naming quick reference

- **Model classes:** singular PascalCase (`UserStoryInteraction`). **DbSets:** plural (`UserStoryInteractions`).
- **Enums:** suffix `...Enum` to avoid colliding with lookup-table model classes (`StoryStatusEnum` the
  enum vs `StoryStatus` the lookup model). Underlying type `: byte`, 0-indexed, stored as `smallint`.
- **String-key constants:** `public const string` in `SiteConstants.cs` (e.g. `SiteBadges.FirstStory = "first-story"`).
- **Lookup-table keys:** `...Key` suffix signals a string PK (`SearchModeKey`, `BadgeKey`); `...Id` implies `int`.
- **Indexes:** `ix_{table}_{columns}` via `HasDatabaseName()`.
- Full deliberated table/column names are in spec §7 — when adding to an existing area, match the spec's chosen name.
