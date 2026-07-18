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
- **Global-flip note (WU-GlobalFlip, 2026-07-13).** L5 stays N/A by design: Identity pages remain
  static SSR, reached via full-document navs since the Server assembly left the interactive router
  (unmatched URLs fall back to full-document loads — the deliberate escape hatch). The flip's
  browser wave found and fixed every `/Account/*` page 500ing: `ReaderDisplayProvider` (renders on
  static-SSR Identity pages too) used `[PersistentState]`, whose persistence callback has no
  inferable render mode on a fully static render — converted to the manual
  `PersistentComponentState` API with explicit `RenderMode.InteractiveAuto` (workplan WU-GlobalFlip
  bug 7). `/Account/Manage` verified live post-fix via full-doc nav.

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

## WU-Email Stage note (2026-07-06) — F1 L2 remains Stage 5, `IdentityNoOpEmailSender` beta
blocker closed

`IdentityNoOpEmailSender` (the only registered `IEmailSender<User>`) meant `RequireConfirmedAccount
= true` blocked every real registration — the sharpest beta blocker (`middle_plan_v2.md` Phase 1
item 5). Closed with a provider-agnostic **SMTP** seam, mechanism settled the same day (see
`middle_plan_v2.md` Resolved "Email mechanism"):

- **`EmailOptions`/`EmailSmtpOptions`** (`Server/Identity/EmailOptions.cs`) bound from `Email`,
  mirroring `S3ImageStorageOptions`'s shape (`SectionName` const, `IOptions<T>`).
- **`SmtpEmailSender`** (`Server/Identity/SmtpEmailSender.cs`, MailKit) implements the same three
  `IEmailSender<User>` methods as `IdentityNoOpEmailSender` — confirmation link, password reset
  link, password reset code. Instrumented per `logging.md`'s reserved `Email` component
  (`CanalaveTelemetry.Email`: `Email.Send` span tagged `canalave.email.kind`, `sent`/`failed`
  counters, no-double-log on failure). Body composition lives in `EmailBodies.cs` (pure, unit-
  tested without a live SMTP connection).
- **Provider switch in `Program.cs`** (`Email:Provider` = `Smtp`/`NoOp`, default `NoOp`) —
  identical shape to the `ImageStorage:Provider` switch. `NoOp` keeps
  `IdentityNoOpEmailSender` registered; its `RegisterConfirmation.razor` on-page confirmation
  link (gated on `EmailSender is IdentityNoOpEmailSender`) needed **no code change** — it
  auto-hides the moment a real sender is active.
- **Mailpit dev inbox** added to `AppHost.cs` (same `AddContainer` shape as Garage; SMTP on 1025,
  web UI on 8025), wired via `Email__Provider=Smtp` + `Email__Smtp__Host/Port` (endpoint-property
  callback form) on the `web` project. Server-only path is unaffected (stays `NoOp`).
- **Scope: transactional only.** Notification email fan-out (`UserNotificationSetting.EmailEnabled`,
  still inert) is explicitly deferred to a follow-up WU — see `audit/Notifications.md`. The hook
  point (`ServerNotificationWriteService.CreateCoreAsync`) is documented there so it isn't
  re-discovered.

**Real bug found and fixed during live verification, not anticipated in the plan:**
`EmailBodies.ConfirmationBody`/`PasswordResetLinkBody` originally re-encoded `confirmationLink`/
`resetLink` via `WebUtility.HtmlEncode`. Every caller (`Register.razor`, `ForgotPassword.razor`,
`ResendEmailConfirmation.razor`, `ExternalLogin.razor`, `Manage/Email.razor`) already wraps the
link in `HtmlEncoder.Default.Encode(...)` before calling `IEmailSender<User>` — the framework-
scaffold contract `IdentityNoOpEmailSender` already relied on (it interpolates the link verbatim).
Encoding it a second time turned the link's already-escaped `&amp;` (the `userId`/`code` query
separator) into `&amp;amp;`, which survives exactly one round of browser HTML-decoding as the
literal text `&amp;` rather than a real `&` — the query-string parser then never sees a second
parameter and `code` fails to bind. **Confirmed against the live Aspire+Mailpit run**: a user
registered before the fix (`email_confirmed = f` after clicking its link) vs. a user registered
after the fix (`email_confirmed = t`) — `psql` ground truth, not page text. Fixed by removing the
re-encode from the two link-body methods (`resetCode`, the one value no caller pre-encodes, keeps
its `HtmlEncode` call). Regression test:
`Tests.Unit/EmailBodiesTests.cs::ConfirmationBody_DoesNotReEncodeAnAlreadyEncodedLink`.

**How verified:** Unit (`EmailOptionsTests.cs` — binding/defaults; `EmailBodiesTests.cs` — body
composition, verbatim link passthrough, the double-encoding regression, reset-code encoding).
Integration (`EmailProviderSelectionTests.cs` — DI resolves `IdentityNoOpEmailSender` under
default config; the `Smtp` branch is **not** integration-tested, deliberately, for the same reason
`ImageStorage:Provider`'s `S3` branch isn't — both switches read `builder.Configuration` directly
in `Program.cs`'s top-level code, before `WebApplicationFactory`'s `ConfigureAppConfiguration`
override can take effect; proving the alternate branch means booting the real process with the env
var set before start). Manual/browser band (Aspire path + Mailpit, real SMTP, no mocks, 2026-07-06):
registered a throwaway user → confirmation email landed in Mailpit (`From` = configured
`Email__FromName`/`FromAddress`) → clicked the decoded link → `email_confirmed` flips true in
Postgres. Forgot-password → reset email landed in Mailpit → clicked the link → set a new password
→ logged in with it (fresh `.AspNetCore.Identity.Application` cookie issued). Email-change reuses
the identical `SendConfirmationLinkAsync`/`ConfirmationBody` path already proven twice, so it was
not separately re-driven. `dotnet test` 1344/1344 (491 Unit / 450 RazorComponents / 403
Integration). Rule: `identity-and-authorization.md` "Identity & Auth"; `run-server/SKILL.md` "Aspire path" +
"Email ground truth".

## WU38a Stage note (2026-07-11) — F1 L2/L3-Logic/L3.5 re-verified (additive); F52 L4-Style → Stage 5

**Scope actually delivered** (the workplan's original "52 L3/L3.5" framing was stale — see
`workplan.md` WU38a for the reconciliation): (A) `DeletePersonalData.razor` gained a delete-vs-
anonymize consequence disclosure (account permanently deleted; authored content anonymized, not
deleted, with orphaned comment threads reading "[Deleted Comment]"; interaction/social data
permanently deleted) and a dedicated anonymous-accessible `Identity/Pages/AccountDeleted.razor`
goodbye page, replacing the `RedirectToCurrentPage()` call that used to bounce a just-signed-out
user back at the `[Authorize]` Manage page and flash a bare 401 (found in the 2026-07-01 browser
pass, fixed here). (B) Account-status login enforcement, folded in from the deferred follow-up
noted after WU39 — see `canalave-conventions/security.md` "Account-Status Enforcement" for the
full mechanism: `CanalaveSignInManager : SignInManager<User>` overrides `CanSignInAsync` to block
Banned and currently-Suspended users at the single choke point every sign-in path shares
(`.AddSignInManager<CanalaveSignInManager>()` in `Program.cs`, after `AddApiEndpoints()` so it
wins); `Login.razor` turns the resulting `SignInResult.NotAllowed` into a specific suspended/banned
message (re-checking the password first, so a wrong-password guess against a blocked account still
gets the generic message); `ServerModerationWriteService.ApplyAccountActionAsync`
(`audit/Moderation.md` Feature 47) bumps the security stamp on Suspend/Ban only, killing an
already-open session via the existing 30-minute `IdentityRevalidatingAuthenticationStateProvider`
revalidation; `ApplicationUserClaimsPrincipalFactory` bakes a new `canalave:account_status` claim
(`ActiveUserClaimTypes.AccountStatus`) alongside the existing baked claims, consumed by the new
`AccountStatusBanner` (SharedUI/Layout, Indicator role) composed into `DesktopLayout`/
`MobileLayout` — renders only for `Warned`.

**Verified:** `dotnet build` 0 warnings/errors (8 projects); `dotnet test` 1483/1483 green (530
Unit / 517 RazorComponents / 436 Integration). New tests: `AccountStatusEnforcementTests`
(Integration — `CanSignInAsync` direct for Active/Warned/Banned/Suspended-future/Suspended-past;
`SignInManager<User>` resolves to `CanalaveSignInManager` in the real DI graph;
`CheckPasswordSignInAsync` surfaces `NotAllowed`/`Succeeded` correctly without touching HttpContext
— the same HttpContext-free path `PreSignInCheck` uses; `ApplyAccountActionAsync` stamp-bump
behavior for Suspend/Ban vs. Warn; claims factory bakes the new claim), `AccountStatusBannerTests`
(RazorComponents — no render anonymous/Active/no-claim, renders for Warned). Mutation-sanity:
temporarily inverted the Banned branch in `CanalaveSignInManager.CanSignInAsync` → 4 tests failed
as expected; reverted, suite green again.

**Manual/browser band (2026-07-11, server-only path, real browser + psql ground truth):**
end-to-end against a throwaway registered-and-confirmed user (`WU38aThrowaway`) — real password
login → `DeletePersonalData` renders the disclosure correctly under the design tokens → delete →
lands on `/Account/AccountDeleted` with no 401 flash → `psql` confirms the `AspNetUsers` row is
gone. Login enforcement driven against the `ReaderGamma` fixture (state restored to Active
afterward): suspended-until-a-future-date → real login POST blocked with "This account is
suspended until {date}."; banned → blocked with "This account has been permanently banned.";
warned → login succeeds and `AccountStatusBanner` renders in the real layout ("Your account has
received a moderator warning…"). Active-session-kill-via-stamp-bump is Integration-tested only
(same "what stays manual" carve-out as `SecurityStampValidator` timing elsewhere in this file) —
proving it live means waiting out the real 30-minute revalidation window, not attempted here.

**Test tier:** Integration (`AccountStatusEnforcementTests.cs`) + RazorComponents
(`AccountStatusBannerTests.cs`). F52 L4-Style flips **Stage 1 → 5** (visual sign-off of the
disclosure + goodbye page, above). F1 L2/L3-Logic/L3.5 stay Stage 5, re-verified — additive, no
Stage change. F1 L4-Style is unchanged (Stage 1) — this WU visually confirmed only the new Login
error copy and the banner, not a full Identity-feature sign-off. Rule:
`canalave-conventions/security.md` "Account-Status Enforcement".

### WU-AuditFixPass note (2026-07-18)

MA-102 closed: the unreferenced `User.Roles` navigation (which minted a phantom `user_id` shadow-FK
column + index on `asp_net_roles`) is deleted; migration `MA102_DropPhantomUserRolesShadowFk` drops
the column/index (verified drop-only). MA-103 closed: `options.User.RequireUniqueEmail = true` in
Program.cs ratifies the already-UNIQUE `EmailIndex` as site policy — duplicate-email registration
now fails as a friendly validation error instead of a raw DbUpdateException 500; intent comment
added at the `HasIndex` site in `IdentityConfigurations`. Full detail: `workplan.md` WU-AuditFixPass.
