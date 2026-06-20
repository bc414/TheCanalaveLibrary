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

## The post-move reconciliation (drives Stage 4 across L2/L3/L3.5)

The Identity folder was physically relocated (`Components/Account` → `Identity/`, per recent git
commits), but several references still point at the old location:
- `IdentityRedirectManager`, `IdentityNoOpEmailSender`, `IdentityRevalidatingAuthenticationStateProvider`
  still declare `namespace TheCanalaveLibrary.Server.Components.Account`.
- `IdentityComponentsEndpointRouteBuilderExtensions` `using`s `...Components.Account.Pages[.Manage]`.
- `App.razor` loads `@Assets["Components/Account/Shared/PasskeySubmit.razor.js"]` — that disk path no
  longer exists (the file is at `Identity/Shared/PasskeySubmit.razor.js`).
- `Program.cs` `using TheCanalaveLibrary.Server.Components.Account;`.

C# namespaces need not match folders, so this **may compile**; the asset path is a runtime 404 and the
whole area is mid-refactor. **Nature of gap:** code-relationship (mechanical). **Implied resolution:**
Stage 2/3 — normalize namespaces + the `App.razor` asset path, then verify build.

---

## Feature 1 — Identity & Auth
- **L1 — Stage 5.** `User`/`ApplicationRole` with `int` keys (Axiom #4); JSON settings via
  `ComplexProperty(...).ToJson()` with enum→short conversions (migrated from the older `OwnsOne` approach
  — see `layer1-data-model.md` §"JSON Complex Types"); role seeding (User/Moderator/Admin) via `HasData`.
  Sound; migration-verified (2026-06-20). *Minor:* `ReaderSettings.DefaultSearchSort` is typed
  `DefaultSortOrder`, which is itself a divergent enum (see Lookups audit) — a thread to pull when the sort
  model is reconciled.
- **L2 — Stage 4.** Cookie auth configured for 401/403 (correct, §1), `RequireConfirmedAccount`, password
  policy, `AddIdentityCore<User>().AddRoles().AddApiEndpoints()`. Reconcile post-move references; verify
  `AddIdentityCore` (vs `AddIdentity`) is the intended choice for the scaffolded UI flow.
- **L3-Logic — Stage 4.** Scaffold uses form-POST-to-endpoint + `SignInManager` cookie writes (matches
  §9.5). Missing: login/logout as triggers on the persistent layout (§3.19); layout is still template
  default.
- **L3.5-Structure — Stage 4.** Pages present and structurally standard; reconcile with the move.
- **L4-Style — Stage 1.** Default Identity/Bootstrap styling; blocked on tokens.
- **L5 — N/A** (Identity is permanently server-only). **L6 — N/A** (framework + `NormalizedName`/
  `NormalizedEmail` unique already configured). **L7/L8 — N/A.**

## Feature 52 — User Account Deletion
- **L1 — Stage 5.** The delete-policy graph is the most deliberate part of `OnModelCreating`: Cascade for
  personal data, `SetNull` to anonymize authored content (breaking diamond conflicts), `Restrict` where C#
  must intervene (profile comments, followed-user, notification source). Comments explicitly flag
  "CONFLICT: Solved with C# code."
- **L2 — Stage 4.** `UserDeletionService` exists to resolve the `Restrict` conflicts before deletion, but
  its correctness against the full conflict map is unverified in this read-only pass. Resolution → verify
  it covers every `Restrict` edge, then Stage 5.
- **L3-Logic / L3.5-Structure — Stage 4.** Surfaced through the scaffolded `Manage/DeletePersonalData`
  page; inherits the post-move reconciliation. **L4 — Stage 1. L5 — N/A.**
