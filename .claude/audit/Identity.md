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

**Hardening scope settled (2026-07-06, Brian, WU-Security/WU-DataProtection planning):**
- Identity lockout ON (`MaxFailedAccessAttempts = 5`, 15-min window, `AllowedForNewUsers`;
  `Login.razor` flips to `lockoutOnFailure: true`) — per-account brute-force defense.
- Auth form posts (`POST /Account/*`) get a per-IP HTTP rate-limit window (10/min) — per-IP
  axis, complements lockout. Per-IP is dev/test-correct now; only meaningful in prod after
  Phase 7's ForwardedHeaders work (see `security.md` §"HTTP Edge Rate Limiting").
- Application-cookie flags set explicitly (`HttpOnly`, `SecurePolicy=Always`, `SameSite=Lax`).
- Data Protection keyring persists via `PersistKeysToDbContext<ApplicationDbContext>` +
  `SetApplicationName` (F1 L2 scope; keys table rides `ApplicationDbContext`'s migration tree);
  deliberately no `ProtectKeysWith*` — rationale in `security.md` §"Data Protection Keyring".
  One-time global sign-out expected when this ships (new key ring + app name).
Conventions: `canalave-conventions/security.md`.

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

## WU-Security + WU-DataProtection Stage note (2026-07-06) — F1 L2 remains Stage 5, hardened

Built per the settled note above; all landed 2026-07-06:
- **Data Protection keyring → Postgres:** `AddDataProtection().PersistKeysToDbContext
  <ApplicationDbContext>().SetApplicationName("TheCanalaveLibrary")`; migration #20
  `AddDataProtectionKeys` (`data_protection_keys`, Respawn-ignored — never delete rows). The
  key repository resolves `ApplicationDbContext` in a fresh DI scope outside any circuit —
  verified safe (`ServerActiveUserContext` stores its deps and resolves the principal lazily;
  write context carries no query filters).
- **Lockout:** `MaxFailedAccessAttempts = 5` / 15 min / `AllowedForNewUsers`;
  `Login.razor` → `lockoutOnFailure: true` (its `IsLockedOut` branch already routed to
  `/Account/Lockout`).
- **Cookie flags explicit:** `HttpOnly`, `SecurePolicy = Always`, `SameSite = Lax`.
- **Per-IP HTTP window on `POST /Account/*`:** 10/min, bodied 429 + `Retry-After` (body is
  load-bearing — `UseStatusCodePagesWithReExecute` re-executes body-less errors).

**How verified:** Integration — `DataProtectionPersistenceTests` (protect/unprotect + keys-row;
cross-factory unprotect = the automated survive-redeploy analog), `AuthRateLimitTests` in
`HttpRateLimitTests` (11th POST → 429 with `Retry-After` + body; GETs unlimited),
`SecurityHeadersTests`. Manual band (real browser, server-only path, 2026-07-06): filesystem
key store moved aside → server process replaced → TestUser session survived and logout form
POST succeeded (antiforgery valid; no filesystem store recreated — keyring provably Postgres);
five wrong ReaderGamma passwords → `/Account/Lockout` (psql-verified counter mid-drill;
lockout state reset afterward). Expect exactly one global sign-out when this ships (new key
ring + app name). Conventions: `canalave-conventions/security.md`.

## L4.5-Browser verification (2026-07-01) — F1 + F52 → Stage 5, three bugs fixed same-session

Full real-form pass (not the dev bar): logout via UserMenu → login → register → email-confirm →
login as the new user → delete the account. Three defects found and fixed:

1. **Login by email failed for every account** (`Login.razor`): the scaffold passes `Input.Email`
   to `PasswordSignInAsync`, which treats it as a *username* — and site usernames are display
   handles that never equal the email, while `[EmailAddress]` validation blocks typing a username.
   Fixed: resolve `FindByEmailAsync` first, sign in with the resolved `UserName` (raw-input
   fallback retained).
2. **Registration 500'd on every submit** (`Register.razor`): the scaffold's bare `Activator`
   user carried `ThemeId = 0` → `fk_asp_net_users_themes_theme_id` violation. Also
   `UserName = Input.Email` would have published email addresses as the public display handle
   (comments/profiles/messaging). Fixed: `ThemeId = 1` (migration-seeded default), plus a required
   Username field (3–32 chars, `[a-zA-Z0-9_-]`) distinct from the email.
3. **"An unhandled error has occurred" banner permanently visible on Identity pages**
   (`App.razor`): the scoped-CSS bundle href said `TheCanalaveLibrary.styles.css` but the real
   asset is assembly-named `TheCanalaveLibrary.Server.styles.css` → 404 → every `*.razor.css`
   rule dead, including `MainLayout.razor.css`'s `#blazor-error-ui { display:none }`. Fixed href.

**Verified:** register ThrowawayUser → RegisterConfirmation shows the NoOp-sender confirm link →
ConfirmEmail succeeds → login by email lands authenticated with baked claims (whoami probe:
nameidentifier/theme/mature all correct) → `/Account/Manage/DeletePersonalData` with password
deletes the account (user row + cascaded `user_stats` gone via psql; anonymous session after).
Known rough edge (not blocking): the post-deletion redirect surfaces a bare 401 page before the
user lands anonymous — deliberate cookie 401/403 config; polish belongs to Identity L4.
Browser-automation note: Blazor SSR forms only serialize values typed via real key events —
programmatic `form_input` values post empty (matters for future browser passes, not for users).
**Correction (2026-07-02):** the above was a misattribution — re-tested in a healthy (non-frozen)
tab, `form_input` values serialize into the SSR Login POST and authentication succeeds. The
original failure was Chrome throttling a backgrounded tab, not the tool or Blazor. Current
guidance: `run-server/SKILL.md` §"Driving the UI reliably".
