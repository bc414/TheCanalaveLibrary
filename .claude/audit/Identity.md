# Audit — Identity/

**Features:** 1 (Identity & Auth), 52 (User Account Deletion). **Exception cluster** — uses
`UserManager`/`SignInManager`/`HttpContext` directly, not the `IXReadService`/`IXWriteService` pattern.
Server-project only; permanently **L5 N/A**.

## Shared Context

**Entities:** `User : IdentityUser<int>` (Core/Identity/), `ApplicationRole : IdentityRole<int>`
(Core/Models/). `User` carries hot filter `ShowMatureContent`, `ThemeId`, `PrefersAnimatedSprites`,
`PrefersDataSaverMode`, `AllowDiscoveryFromHiddenFavorites`, and three owned JSON columns
(`ReaderSettings`, `PrivacySettings`, `AuthorSettings`) mapped via `OwnsOne(...).ToJson()` with inner
enum→short conversions. Cold `UserProfile` partition is 1-to-1.

**Pages (Server/Identity/Pages + /Shared):** full default Blazor-Identity scaffold (Login, Register,
2FA, recovery, ExternalLogin, Manage/*, ConfirmEmail, ResetPassword, Passkeys, …). Plus
`IdentityRedirectManager`, `IdentityRevalidatingAuthenticationStateProvider`, `IdentityNoOpEmailSender`,
`IdentityComponentsEndpointRouteBuilderExtensions`.

**Services:** `UserDeletionService` (Server). DI + cookie 401/403 config + role seeding in `Program.cs`.

---

## The post-move reconciliation — RESOLVED (2026-06-20, WU1)

The Identity folder was physically relocated (`Components/Account` → `Identity/`). Namespaces
(`IdentityRedirectManager`, `IdentityNoOpEmailSender`, `IdentityRevalidatingAuthenticationStateProvider`,
all Identity `.razor` pages) now declare `TheCanalaveLibrary.Server` consistently; the
`App.razor` asset path reads `Identity/Shared/PasskeySubmit.razor.js`; the endpoint extensions'
`using`s already resolved without changes (page classes share the flat `TheCanalaveLibrary.Server`
namespace). Only a stale comment remained (fixed). `dotnet build` green; `/Account/Login` and
`/Account/Register` confirmed 200 against the live app.

---

## Feature 1 — Identity & Auth
- **L1 — Stage 5.** `User`/`ApplicationRole` with `int` keys (Axiom #4); JSON settings via
  `ComplexProperty(...).ToJson()` with enum→short conversions (migrated from the older `OwnsOne` approach
  — see `layer1-data-model.md` §"JSON Complex Types"); role seeding (User/Moderator/Admin) via `HasData`.
  Sound; migration-verified (2026-06-20). *Minor:* `ReaderSettings.DefaultSearchSort` is typed
  `DefaultSortOrder`, which is itself a divergent enum (see Lookups audit) — a thread to pull when the sort
  model is reconciled.
- **L2 — Stage 5.** Cookie auth configured for 401/403 (correct, §1), `RequireConfirmedAccount`, password
  policy, `AddIdentityCore<User>().AddRoles().AddApiEndpoints()`. Post-move references resolved (see
  above). `AddIdentityCore` (vs `AddIdentity`) confirmed as the intended choice for the scaffolded UI flow.
- **L3-Logic — Stage 5.** Scaffold uses form-POST-to-endpoint + `SignInManager` cookie writes (matches
  §9.5). §3.19 login/logout triggers added as a minimal `LoginDisplay.razor` leaf (SharedUI/Layout) — an
  `<AuthorizeView>` showing the username + a logout form-POST when authenticated, a "Log in" link to
  `/account/login` when not — composed into `DesktopLayout`/`MobileLayout` and the Identity `MainLayout`
  (the Manage section's nested `@layout`, found live via `ManageLayout` → `MainLayout` chaining, not
  debris). **Scope note:** this is the minimal trigger only — full nav links, notification bell, and
  profile avatar dropdown are deferred to WU22/WU30/WU33 per §3.19's fuller spec.
- **L3.5-Structure — Stage 5.** Pages present and structurally standard; reconciled with the move.
  Verified live: anonymous home page renders `<a href="Account/Login">Log in</a>`.
- **L4-Style — Stage 1.** Default Identity/Bootstrap styling; blocked on tokens (unchanged by WU1).
- **L5 — N/A** (Identity is permanently server-only). **L6 — N/A** (framework + `NormalizedName`/
  `NormalizedEmail` unique already configured). **L7/L8 — N/A.**

## Feature 52 — User Account Deletion
- **L1 — Stage 5.** The delete-policy graph is the most deliberate part of `OnModelCreating`: Cascade for
  personal data, `SetNull` to anonymize authored content (breaking diamond conflicts), `Restrict` where C#
  must intervene (profile comments, followed-user, notification source). Comments explicitly flag
  "CONFLICT: Solved with C# code."
- **L2 — Stage 5 (2026-06-20, WU1).** `UserDeletionService` now resolves all four User-rooted `Restrict`
  edges before deleting: `Notification.SourceUserId` (SetNull, pre-existing), `FollowedUser.FollowedUserId`
  (delete, pre-existing), `UserProfileComment.ProfileUserId` (delete — was dead/commented-out code
  referencing a non-existent `_context.UserProfileComments` DbSet; fixed to
  `_context.BaseComments.OfType<UserProfileComment>()`, the correct TPT access pattern), and
  `Vouch.VouchedUserId` (delete — previously unhandled entirely). Registered in DI
  (`AddScoped<UserDeletionService>()`).
  **Bug found by running it for real:** `AddNpgsqlDbContext` enables Npgsql's retrying execution
  strategy, which rejects a manually-`BeginTransactionAsync`'d transaction unless the whole retriable
  unit runs through `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`. Fixed. No other
  code in the tree uses explicit transactions yet, but any future direct `BeginTransactionAsync()` call
  against `ApplicationDbContext`/`ReadOnlyApplicationDbContext` must use this pattern.
  **Verified:** exercised twice end-to-end via the new `DevDiagnosticsEndpoints.MapDevDiagnosticsEndpoints`
  (`/dev/test-delete-user/{id}`, Development-only — see `.claude/skills/run-server/SKILL.md` "Dev
  diagnostics endpoints") against throwaway fixture users (each given a profile comment, a received
  vouch, an inbound follow, and a sourced notification via direct `psql` inserts) — confirmed all four
  edges resolved and the user row gone, both runs.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `UserDeletionServiceTests`
  in `TheCanalaveLibrary.Tests.Integration` (tier: **Integration**). Tests create throwaway users via
  `UserManager<User>` (ThemeId=1, migration-seeded) and verify: unknown userId → false; existing user
  → true with user row and cascade-deleted `UserStat` gone; `Notification.SourceUserId` nulled (not the
  notification deleted — recipient history preserved); `FollowedUser` (FollowedUserId=target) removed;
  `Vouch` (VouchedUserId=target) removed; `UserProfileComment` (ProfileUserId=target) removed — all
  without FK violation against a real Testcontainers Postgres. Auth/claims band (cookie/claims paths
  through `AccountController`) remains manual-only. Mutation-sanity confirmed: commenting out the
  `Notification` SetNull step → `DeleteUserAsync_NullsOutSourceUserId_OnNotificationsSentByThisUser`
  fails with an FK violation. `dotnet test` green.
- **L3-Logic / L3.5-Structure — Stage 5.** `DeletePersonalData.razor` rewired from the old direct
  `UserManager.DeleteAsync(user)` (which would throw on the `Restrict` FKs) to
  `DeletionService.DeleteUserAsync(user.Id)`; a `false` return (not found) now redirects to the existing
  invalid-user path instead of throwing. Inherits the resolved post-move reconciliation. **L4 — Stage 1
  (unchanged). L5 — N/A.**
