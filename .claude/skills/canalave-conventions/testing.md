# Testing — All Layers

Conventions for automated tests across the project. "Tier" below refers to test kind
(unit vs. integration vs. component render) — distinct from the architecture's 8 numbered
*Layers* (data model, services, logic, structure, style, WASM, indexes, Redis); a test tier
can exercise code from several of those layers.

## The three test tiers (organized by kind, not by production project)

The fundamental axis is **does the test need a real host or DB** — not which production
project the type-under-test lives in.

| Tier | Project | What it can `new` / spin up | Targets |
|---|---|---|---|
| **Unit** | `TheCanalaveLibrary.Tests.Unit` (references Core **and** Server) | Any type constructed directly — no `WebApplicationFactory`, no Testcontainers, no `DbContext` | Pure logic: `StoryValidations`, `StoryMappers`, `StorySlug.Slugify`; **and** host-free Server services: `ServerHtmlSanitizationService` (no deps), `ServerSpriteReadService` (fake `IWebHostEnvironment`) |
| **Integration** | `TheCanalaveLibrary.Tests.Integration` (references Server) | `WebApplicationFactory<Program>` + Testcontainers Postgres | `Server{Feature}{Read,Write}Service` impls, content-rating query filter, EF projections, unique indexes, `IImageStorageService`, `UserDeletionService` |
| **RazorComponents** | `TheCanalaveLibrary.Tests.RazorComponents` (references SharedUI, Client) | bUnit `TestContext` — renders Razor components without a real host or DB | **L3 `@code` logic only:** EventCallback invocations with correct arg values, service method calls triggered by interaction, non-trivial computed state. Not L3.5 markup structure or L4 style. |

**The placement rule is behavioral, not by production project.** If you can `new` the
type (or small fakes of its deps) and assert without a real host or DB, it is a Unit test
— even if the type lives in `TheCanalaveLibrary.Server` (sanitizer, sprite-resolver).

**One guardrail: no `DbContext` in Unit.** The Unit project now references Server, so EF
types are transitively available — keep them out of Unit tests by convention. If the
test needs a `DbContext`, it needs a real Postgres container → it belongs in Integration.

**Don't mock `DbContext`/`DbSet`** to force DB-touching logic into Unit or RazorComponents.
The invariants worth protecting are Postgres-specific; mocked LINQ isn't evidence.

## Integration tests run against real Postgres — never InMemory/SQLite

The invariants this project most needs protected are **Postgres-specific EF behaviors**, not
generic LINQ:
- The `"ContentRating"` named query filter (`ApplicationDbContext.OnModelCreating`) — whether
  it *translates to SQL* against the real provider, not just evaluates in LINQ.
- The slug unique-filtered index (`StoryConfigurations.cs`,
  `HasFilter("\"slug\" IS NOT NULL")`).
- `EFCore.NamingConventions`' snake_case translation.
- The FTS `SearchVector` generated column + GIN index (once a test needs it).
- Restrict-vs-Cascade FK conflicts (e.g. `UserDeletionService`) — only real Postgres enforces
  the constraint and can prove the manual cleanup order is correct.

EF Core's InMemory provider executes no SQL at all — a query filter "passes" there whether or
not it would ever translate against Postgres. SQLite has different filter/index/type semantics.
Either gives **false confidence on exactly the things worth testing**. Integration tests use
**`Testcontainers.PostgreSql`**: a real, ephemeral Postgres container per test run, migrated
with the actual `InitialSchema` migration (`Database.MigrateAsync()`, not `EnsureCreated()` —
`EnsureCreated` skips migrations entirely and would silently let a broken migration pass).

## Driving the content-rating filter: fake `IActiveUserContext`, not real auth

`ApplicationDbContext`'s query filter closes over an injected `IActiveUserContext` (see
`cross-cutting.md` "Content Rating Filtering" / "Active-User Context"). Integration tests need
to flip `ShowMatureContent`/`UserId`/role flags per-test without a real sign-in flow, cookies,
or `SecurityStampValidator`. Use a `TestAppFactory : WebApplicationFactory<Program>` that does **two** things in
`ConfigureWebHost` via `builder.ConfigureServices(...)`:

1. **Re-registers both `DbContext`s** with the Testcontainers connection string:
   ```csharp
   services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
   services.RemoveAll<DbContextOptions<ReadOnlyApplicationDbContext>>();
   services.AddDbContext<ApplicationDbContext>(opt => opt.UseNpgsql(connectionString)…);
   services.AddDbContext<ReadOnlyApplicationDbContext>(opt => opt.UseNpgsql(connectionString)…);
   ```
   `ConfigureAppConfiguration` is **not** sufficient here: `Program.cs` reads
   `builder.Configuration.GetConnectionString("canalavedb")` before the
   `WebApplicationFactory`'s callback fires (a `WebApplicationBuilder` timing quirk), so the
   in-memory override is too late. Replace the `DbContextOptions` descriptors directly
   in `ConfigureServices` — that fires after the builder is fully constructed.

2. **Replaces `IActiveUserContext`** with `FakeActiveUserContext` (the settable test double).

Everything else (real service registrations, Identity) stays wired as production.

## What stays manual (out of scope for automated tests)

Auth-cookie claim baking (`ApplicationUserClaimsPrincipalFactory`), `SecurityStampValidator`
timing relative to `IActiveUserContext`'s lazy resolution, and SignalR circuit init are
genuinely host/middleware-timing concerns that a fake `IActiveUserContext` deliberately
sidesteps rather than exercises. These remain manual (or a future Playwright pass) — don't try
to force them into the integration tier by removing the fake; that would reintroduce the timing
fragility the lazy-read pattern in `ServerActiveUserContext` exists to avoid.

## What belongs in RazorComponents (bUnit)

**Scope = Layer 3 (`@code` logic) only.** bUnit tests cover what *cannot be verified by reading
the Razor file* — C# computation that produces a behavioral outcome. They do not cover Layer 3.5
(markup structure) or Layer 4 (style), both of which are declarative and human-verified.

**Test these — they involve `@code` computation:**
- EventCallback invocations with the correct argument values (e.g. `OnRemoveVouch` fires with
  the vouched user's id — the caller must pick the right field from the DTO).
- Service method calls triggered by interaction (e.g. `FollowAsync(targetUserId)` called on
  click — the component must resolve the right id from its parameters).
- `disabled` attribute or other state computed in `@code` from a non-trivial condition (e.g.
  a property derived from multiple parameters, not a direct pass-through).

**Do not test these — they are readable from the Razor file:**
- `@if (IsEditable)` / `@if (IsFollowing)` blocks where the condition is a bare `[Parameter]`
  pass-through. The logic is one line; reading it is faster than a test and the test adds no
  new information.
- `@foreach` producing one item per input element — that's declarative markup.
- Presence of static text, placeholder messages, or aria-label values set by string interpolation.
- Any Tailwind class presence — Layer 4, human sign-off only.

**The deciding question:** does the test require running C# code to know what will happen, or
can you determine the answer by reading the `.razor` file? If the latter, skip the test.

**Note on AngleSharp CSS selector fragility:** bUnit uses AngleSharp, which silently ignores
compound selectors when the qualifier is a hyphenated class name (`.text-danger`) or an
attribute-value filter (`[attr^='...']`) — it falls back to matching only the element-type
prefix. Use markup string assertions (`cut.Markup.Contains(...)`) or direct index access
(`cut.FindAll("button")[n]`) instead of compound CSS selectors. Prefer `[aria-label]` presence
selectors or element-type-only selectors when finding elements to click.

**BlazoredTextEditor button collision (WU20 lesson):** Components that embed `EditorView` /
`BlazoredTextEditor` have an extra risk — the editor package renders its own buttons into the same
DOM subtree (toolbar buttons with empty text, and potentially others). Both index-based (`[n]`) and
text-content-based (`First(b => b.TextContent.Trim() == "Save")`) selection can hit editor-package
buttons instead of the host component's buttons. Those editor buttons have no Blazor `@onclick`
binding, so any click test targeting them will throw `MissingEventHandlerException`, and any
attribute check (`HasAttribute("disabled")`) returns false. **Rule: any button on a component that
wraps `EditorView` MUST carry a unique `aria-label` attribute; tests find it with
`cut.Find("button[aria-label='…']")`** — this is the only collision-free selector in that subtree.
(See `CommentEditor.razor` for the pattern: `aria-label="@SaveLabel"` on the save button,
`aria-label="Cancel"` on the cancel button.)

**Does not belong here:** full auth flows, real service calls, or anything requiring a real
server circuit (SignalR). Keep components thin (push pure logic down to Core so Unit covers it).

## Integration tests reset between every test (Respawn)

Each integration test starts from an identical known baseline — the schema produced by the
migration plus lookup-table rows seeded by EF's `OnModelCreating HasData` (status enums,
notification types, ASP.NET role rows). **No application rows survive from one test to the
next.** `PostgresFixture` holds a `Respawner` (from the `Respawn` NuGet package) that issues
FK-ordered deletes between tests; the lookup tables listed in `TablesToIgnore` are never wiped.

Because of this reset, **every test must seed the identities and rows it needs.** Use the
helpers on `IntegrationTestBase`:
- `SeedUserAsync(string? name = null, bool showMature = false)` — creates a GUID-suffixed
  user via `UserManager`, returns the new `UserId`. Never query by username (`"TestUser"`) or
  by `OrderBy(u => u.Id).Take(2)` or hardcode `userId: 1` — `DataSeeder`'s `TestUser`/`AdminUser`
  may exist in the DB (factory uses `Environments.Development`) but no test may rely on them;
  Respawn wipes them before each test and their IDs are non-deterministic.
- `SeedStoryAsync(int? authorId = null)` — creates a minimal story with a GUID-suffixed
  title, returns the new `StoryId`.
- `SetActiveUser(FakeActiveUserContext value)` — swaps the `IActiveUserContext` singleton
  for this test's factory.

Because tests reset cleanly, **absolute assertions are allowed and expected**: exact counts,
ordering from empty, "must be the only row", reject-at-limit. Write the natural test — call
the service N times to reach the limit, then assert the (N+1)th call throws. No top-up logic,
no precondition-seeding-via-DB workaround.

**The DB is shared within a single run (one container, one `PostgresFixture`)**, so tests must
still run serially. This is enforced by `[assembly: CollectionBehavior(DisableTestParallelization
= true)]` — do not remove. The container is ephemeral: `PostgresFixture` starts it, applies the
migration, and disposes it at the end of the run.

## Integration test setup: seed FK parents before testing a write

When a service writes to a table that has FK constraints, all parent rows must exist before
the call. In the real application, prior user navigation creates them (e.g. opening a story
creates a `UserStoryInteraction` row before `RecordAttributionSourceAsync` is ever called). In
integration tests, that prior navigation does not run.

**For every integration test that exercises a service write:**
1. Map every FK on the target table (check the EF configuration file for the cluster).
2. Determine whether the parent row is produced by `SeedUserAsync` / `SeedStoryAsync` /
   a prior step in the same test. If not, seed it explicitly via `ApplicationDbContext`.
3. Add a brief comment on the seed: what real-world flow creates this row and why the test
   must supply it.

Respawn wipes all rows between tests — each test is self-contained in its FK setup; nothing
can be assumed to survive from a previous test.

## Integration test helper pitfall: async methods that create a scope must `await` the call inside it

Any helper method that opens a `using IServiceScope scope` and calls a service **must be `async` and
must `await` the service call** — do not return the bare `Task<T>`:

```csharp
// WRONG — scope disposed before the query finishes streaming
private Task<ChapterReadingDto?> GetForReadingAsync(int storyId, int chapterNumber)
{
    using IServiceScope scope = _factory.Services.CreateScope();
    IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
    return svc.GetChapterForReadingAsync(storyId, chapterNumber); // scope tears down here!
}

// CORRECT
private async Task<ChapterReadingDto?> GetForReadingAsync(int storyId, int chapterNumber)
{
    using IServiceScope scope = _factory.Services.CreateScope();
    IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
    return await svc.GetChapterForReadingAsync(storyId, chapterNumber);
}
```

When the bare `Task` is returned, the C# compiler exits the method body (and therefore the `using`
block) at the `return` statement — before any `await` suspends execution and before the async
database query runs. The `IServiceScope` is disposed, which disposes the `DbContext`, which closes
the Postgres connection. Npgsql then throws `InvalidOperationException: The reader is closed` when
the deferred query eventually tries to stream results. The symptom looks like a data or type error,
not a lifetime error, which makes it easy to misdiagnose.

This applies to any async method with a `using`-scoped resource: `IServiceScope`, `HttpClient`, or
any other `IDisposable` that owns resources the async operation needs.

## Dev-diagnostics endpoints are probes, not the regression net

`DevDiagnosticsEndpoints.cs` (`MapDevDiagnosticsEndpoints`, see `run-server/SKILL.md` "Dev
diagnostics endpoints") is for **interactive, one-off exploration** during local verification —
it's `Development`-only, never runs in CI, and the endpoints themselves assert nothing; a human
reads the JSON and judges. Once a behavior has an asserted test, the test is the source of truth
for that behavior — a dev endpoint can stay as a debugging convenience, but its output is not
proof of correctness on its own. Don't remove a standing dev-diagnostics endpoint or its
fixtures without checking the relevant audit file for a note that they were deliberately kept.

## Project setup reference

- **`TheCanalaveLibrary.Tests.Unit`**: `xunit`, `xunit.runner.visualstudio`,
  `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `coverlet.collector`. Project references: Core
  **and** Server (enables host-free Server-service tests; keep no `DbContext` in Unit tests).
- **`TheCanalaveLibrary.Tests.Integration`**: same xUnit/FluentAssertions packages, plus
  `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`, and `Respawn`. Project
  reference: Server. All test classes inherit `IntegrationTestBase` (provides factory
  lifecycle, `ResetAsync` via `PostgresFixture`, `SeedUserAsync`, `SeedStoryAsync`,
  `SetActiveUser`). Serial execution enforced by `[assembly: CollectionBehavior(
  DisableTestParallelization = true)]` in `AssemblyInfo.cs`.
- **`TheCanalaveLibrary.Tests.RazorComponents`**: `bunit`, `xunit`,
  `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `coverlet.collector`.
  Project references: SharedUI (add Client reference if a tested component lives there).
- All three added to `TheCanalaveLibrary.sln`; `dotnet test` from the repo root runs all tiers.
  `Tests.Unit` and `Tests.RazorComponents` are fast (no container); `Tests.Integration` boots
  `postgres:18-alpine` via `PostgresFixture`.
