# Slice 1 — Foundation (Data, Program/root, Images, Sprites, Lookups, SiteSettings, Security, Diagnostics, DevTools, Components, Http, Telemetry, Seo, Home, legacy folders + atom tests)

Audited 2026-07-17 by the S1 slice agent. Read-only pass; no builds/tests run — all `verify: [pending]`.

## File inventory (path + LOC)

### Product — TheCanalaveLibrary.Server
| LOC | File |
|---|---|
| 562 | Server/Program.cs |
| 9 / 14 | Server/appsettings.json / appsettings.Development.json |
| 27 | Server/Properties/launchSettings.json |
| 11 / 1127 | Server/package.json / package-lock.json (generated — listed, not audited) |
| 207 | Server/Data/ApplicationDbContext.cs |
| 58 | Server/Data/ReadOnlyApplicationDbContext.cs |
| 43 | Server/Data/ApplicationDbContextFactory.cs |
| 784 | Server/Data/DataSeeder.cs |
| 16 | Server/Data/SiteConstants.cs |
| 41/132/66/93/33/112/31/81/234/48/100/149/29/77/18/68/23/361/163/68 | Server/Data/Configurations/{Badge,BlogPost,Chapter,Comment,CustomList,Discovery,Following,Group,Identity,Messaging,Moderation,Notification,Profile,Recommendation,SiteSettings,Spotlight,Sprite,Story,Tag,UserStoryInteraction}Configurations.cs (~1,924 total) |
| 56/179/73/116/19/26/144 | Server/Images/{ImageEndpoints,ImageUploadProcessor,ImageUploadRules,LocalImageStorageService,ProcessedImage,S3ImageStorageOptions,S3ImageStorageService}.cs (613) |
| 24/23/21 | Server/Sprites/{LocalSpriteAssetProbe,ServerThemeReadService,ThemeEndpoints}.cs |
| 31/42/73 | Server/Security/{CspPolicy,SecurityHeadersMiddleware,ServerWriteRateLimitService}.cs |
| 161 | Server/Diagnostics/DevDiagnosticsEndpoints.cs |
| 105/36/9/31/161/63/10 | Server/Components/{App.razor,Error.razor,Layout/MainLayout.razor,ReconnectModal.razor,ReconnectModal.razor.css,ReconnectModal.razor.js,_Imports.razor} |
| 77 | Server/Http/EndpointHelpers.cs |
| 69 | Server/Telemetry/TelemetryCircuitHandler.cs |
| 27/38/59 | Server/SiteSettings/{ServerSiteSettingsReadService,ServerSiteSettingsWriteService,SiteSettingsEndpoints}.cs |
| 30/84 | Server/Services/{ServerDeviceDetectionService,UserDeletionService}.cs (LEGACY folder) |
| 9/18 (approx.) | Server/Pages/_Host.cshtml, Server/Pages/Shared/_Layout.cshtml (LEGACY folder, dead — MA-105) |
| 17/1553 | Server/ReferenceSQL/{AlsoFavoritedStaging,CanalaveDBCreation}.sql — **scope ambiguity:** CanalaveDBCreation.sql is the pre-EF SQL-Server-era schema reference (historical artifact, superseded by migrations); AlsoFavoritedStaging.sql is mart reference SQL (S7's domain). Listed, skimmed, not line-audited. |

### Product — TheCanalaveLibrary.Core
| LOC | File |
|---|---|
| 38 | Core/Images/IImageStorageService.cs |
| 30/17/17/24/20/22/9 | Core/Sprites/{ISpriteAssetProbe,ISpriteReadService,IThemeReadService,OptimisticSpriteReadService,Theme,ThemeContext,ThemeDto}.cs |
| 280/16 | Core/Lookups/{ModelEnums,StoryStatus}.cs |
| 16/13/23/45 | Core/SiteSettings/{ISiteSettingsReadService,ISiteSettingsWriteService,SiteSetting,SiteSettingKeys}.cs |
| 20/25/23 | Core/Security/{IWriteRateLimitService,WriteActionKind,WriteRateLimitExceededException}.cs |
| 279 | Core/Diagnostics/CanalaveTelemetry.cs |
| 26/29/54 | Core/Seo/{IPublicUrlProvider,PublicUrlProvider,SocialDescriptionHelper}.cs |
| 14/14/14/26/18/24 | Core/Models/{AcknowledgmentRole,BetaReader,CoAuthor,FeatureContribution,StoryAcknowledgment,UserCustomFilter}.cs (LEGACY folder) |
| 5 | Core/ServiceInterfaces/IDeviceDetectionService.cs (LEGACY folder) |
| 11 | Core/Http/PagedResult.cs — **scope ambiguity:** Core/Http not named in the brief; included as Http-cluster foundation |

### Product — SharedUI / Client / ServiceDefaults / AppHost
| LOC | File |
|---|---|
| 8/3 | SharedUI/_Imports.razor, SharedUIAssemblyIdentifier.razor |
| 37 | SharedUI/Sprites/ThemeContextProvider.razor |
| 53 | SharedUI/Seo/SocialMetaTags.razor |
| 13/18/15 | SharedUI/Home/{HomePage,HomeDesktop,HomeMobile}.razor |
| 20 | SharedUI/Pages/NotFound.razor (LEGACY folder) |
| 245 | SharedUI/DevTools/DesignGalleryPage.razor — **scope ambiguity:** brief listed DevTools under Server; it lives in SharedUI; included |
| 110/29/10/9/3 | Client/{Program.cs,Routes.razor,RedirectToLogin.razor,_Imports.razor,WasmClientAssemblyIdentifier.razor} |
| 62 | Client/Http/ClientHttpHelpers.cs |
| 29 | Client/Services/WasmDeviceDetectionService.cs (LEGACY folder) |
| 172 | ServiceDefaults/Extensions.cs |
| 106/21/17/30 | AppHost/{AppHost.cs,garage.toml,appsettings×2,Properties/launchSettings.json} |
| 102 | scripts/check-design-tokens.ps1 (read as H-05 evidence) |

### Tests owned by this slice
| LOC | File |
|---|---|
| 8/98/136/127/37/18 | Tests.Integration/{AssemblyInfo,PostgresFixture,IntegrationTestBase,TestAppFactory,FakeActiveUserContext,FakeWriteRateLimitService}.cs |
| 225/59/40/97/39/41 | Tests.Integration/{ContentRatingFilterTests,DataProtectionPersistenceTests,EmailProviderSelectionTests,ConcurrentReadAccessTests,NpgsqlTracingSmokeTests,HostBootTests}.cs |
| 92/103/156/51 | Tests.Integration/{GarageFixture,ImageStorageServiceTests,S3ImageStorageServiceTests,TestImages}.cs |
| 68/83/130 | Tests.Integration/{SecurityHeadersTests,HttpRateLimitTests,WriteThrottleTests}.cs |
| 49/162/160/72/259/174/97/103/90/100 | Tests.Unit/{CspPolicyTests,HtmlSanitizationServiceTests,ImageStorageTelemetryTests,ImageTestSupport,ImageUploadProcessorTests,SpriteReadServiceTests,PublicUrlProviderTests,SocialDescriptionHelperTests,ServerWriteRateLimitServiceTests,ExceptionPresenterTests}.cs |
| 70/135/122/78/108/60/254/72/156 | Tests.RazorComponents/{AccountStatusBanner,CanalaveErrorBoundary,CanalaveTypeahead,ContentSurface,DraftAutosave,InlineAlert,PaginationControls,ToastHost,UserCard}Tests.cs |

Scope notes: `Server/Endpoints/` no longer exists (fully migrated — brief listed it as legacy). No Sprite/SiteSettings/Seo *integration* tests exist (Unit-tier only — consistent with testing.md's placement rule). `MartsTelemetryTests`/`EmailBodiesTests` skimmed and assigned to S7/S6.

---

### MA-101 | Tier 1 | Bucket A | Slice 1
claim: ReconnectModal's co-located JS module is referenced at a stale physical path (`Components/Layout/…` — the file lives at `Components/…`), so `@Assets` misses the manifest, the script 404s silently, and the .NET 10 reconnect-modal UI (circuit-drop "Rejoining the server…" dialog) never activates — exactly the silent-404 folder-move failure SKILL.md's co-located-asset rule warns about.
evidence: `TheCanalaveLibrary.Server/Components/ReconnectModal.razor:2` — `<script type="module" src="@Assets["Components/Layout/ReconnectModal.razor.js"]"></script>` vs. actual file path `TheCanalaveLibrary.Server/Components/ReconnectModal.razor.js` (no `Layout/` copy exists; verified by directory listing). SKILL.md §"Enforcing the Flat Namespace": "Co-located component assets … are referenced via `@Assets["PhysicalFolderPath/Component.razor.js"]` — the *physical* folder path … Folder renames break these silently (404 at runtime, no compile error)."
cells: cross-cutting layout chrome (WU-ErrorHandling's ReconnectModal surface; status.md Global Conditions "Error-handling strategy live" — no single cell; touches every interactive page's disconnect UX)
effort: S | route: mechanical sweep (fix the two path strings; browser-verify the dialog on a forced circuit drop)
verify: [pending]

### MA-102 | Tier 2 | Bucket A | Slice 1
claim: `User.Roles` (`ICollection<ApplicationRole>`) is an unreferenced navigation that EF models as a spurious one-to-many, minting a phantom nullable `user_id` shadow-FK column + index on `asp_net_roles` — the same phantom-shadow-FK class layer1-data-model.md documents for TPT navs; it contradicts Identity's many-to-many role model (a role row can be "owned" by one user), violates "Delete behavior is always explicit" (no `.OnDelete` exists for this accidental relationship), and would corrupt shared role rows if any code ever populated it.
evidence: `TheCanalaveLibrary.Core/Identity/User.cs:160` — `public virtual ICollection<ApplicationRole> Roles { get; set; } = new List<ApplicationRole>();` ; migration snapshot (asp_net_roles) — `b.Property<int?>("UserId") … .HasColumnName("user_id");` + `b.HasIndex("UserId").HasDatabaseName("ix_asp_net_roles_user_id");` ; repo-wide grep for `.Roles` usage: zero hits. No settled note found in `audit/Identity.md`.
cells: F1 L1 — **proposes reopen** (status.md row 1 L1 = Stage 5)
effort: M | route: Stage-4 reconcile (delete the nav + migration dropping `user_id`/index; User entity is S6's file — cross-slice handoff noted)
verify: [pending]

### MA-103 | Tier 2 | Bucket A | Slice 1
claim: `UserConfiguration`'s unnamed `HasIndex(e => e.NormalizedEmail).IsUnique()` silently merges with and mutates Identity's built-in non-unique `EmailIndex` into a UNIQUE index (H-03's silent-overwrite mechanism), while Identity options never set `RequireUniqueEmail` — so a duplicate-email registration bypasses UserManager validation and surfaces as a raw `DbUpdateException` (unhandled 500 / broken register flow) instead of a friendly validation error.
evidence: `TheCanalaveLibrary.Server/Data/Configurations/IdentityConfigurations.cs:192-193` — `builder.HasIndex(e => e.NormalizedUserName).IsUnique();` / `builder.HasIndex(e => e.NormalizedEmail).IsUnique();` ; migration snapshot — `b.HasIndex("NormalizedEmail").IsUnique().HasDatabaseName("EmailIndex");` (Identity's default EmailIndex is non-unique) ; `TheCanalaveLibrary.Server/Program.cs:126-143` — the `AddIdentityCore<User>(options => …)` block sets SignIn/Password/Lockout options only, no `options.User.RequireUniqueEmail = true`.
cells: F1 L1 + L2 — **proposes reopen** (both Stage 5). Register page behavior is S6's surface; the schema/options mismatch is foundation.
effort: S | route: Stage-4 reconcile (either set `RequireUniqueEmail = true` to match the DB constraint, or drop the unique index — direction is a product decision)
verify: [pending]

### MA-104 | Tier 2 | Bucket A | Slice 1
claim: identity-and-authorization.md's load-bearing "MVP posture (everything requires login)" — `AddAuthorization(options => options.FallbackPolicy = RequireAuthenticatedUser)` — is implemented nowhere; the app runs the default-allow (post-MVP) posture without the doc's mandated companion step for that flip ("re-audit every endpoint's `.RequireAuthorization()` … the two surfaces must move together"), so either the doc is stale or a documented security posture is silently missing. Symmetric — direction undetermined.
evidence: repo-wide grep `FallbackPolicy|AddAuthorization` — zero hits in Server (only Client's `AddAuthorizationCore()` and bUnit helpers) ; `identity-and-authorization.md` §"Default-Deny for MVP" — "**MVP posture (everything requires login):** `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();`". `audit/Identity.md:67` treats anonymous browsing as verified-normal ("Verified live: anonymous home page renders `<a href="Account/Login">Log in</a>`") but records no posture decision.
cells: F1 all layers (doc-vs-code; H-11)
effort: S | route: doc-touch decision (update the doc to state default-allow is the operative posture + record the per-endpoint audit status, or implement the fallback policy)
verify: [pending]

### MA-105 | Tier 2 | Bucket A | Slice 1
claim: The Razor Pages host remnant is dead wiring that misdescribes the composition root: `AddRazorPages()` is registered with a comment claiming it backs an `_Host.cshtml` fallback, but `MapRazorPages`/`MapFallbackToPage` is never called, making `Server/Pages/_Host.cshtml` + `Pages/Shared/_Layout.cshtml` unreachable (and internally rotten — `_Host` targets a namespace no type declares and render-mode `WebAssembly`, both pre-dating the current `App.razor` host).
evidence: `TheCanalaveLibrary.Server/Program.cs:30-31` — "// Add services for Razor Pages, which are required for the _Host.cshtml fallback." / `builder.Services.AddRazorPages();` ; grep `MapRazorPages|MapFallback` — zero hits ; `Server/Pages/_Host.cshtml:2,9` — `@using TheCanalaveLibrary.Server.Components` (namespace declared by no type; grep zero hits) / `<component type="typeof(App)" render-mode="WebAssembly" />`.
cells: F1 organization / composition root (legacy-folder disposition: DELETE `Server/Pages/` + the `AddRazorPages()` line + comment)
effort: S | route: mechanical sweep
verify: [pending]

### MA-106 | Tier 3 | Bucket A | Slice 1
claim: `.AddApiEndpoints()` on the Identity builder registers the `MapIdentityApi` support services (incl. bearer-token infrastructure), but `MapIdentityApi` is never mapped — the call's only live effect is the `SignInManager` registration that `.AddSignInManager<CanalaveSignInManager>()` immediately overrides anyway, so it is likely vestigial service surface (needs a compile/run check before removal).
evidence: `TheCanalaveLibrary.Server/Program.cs:146` — `.AddApiEndpoints() // Add the new .NET 8 Identity API endpoints` ; grep `MapIdentityApi` — zero hits ; `Program.cs:148-152` — the `.AddSignInManager<CanalaveSignInManager>()` comment: "Overrides AddApiEndpoints's SignInManager<User> registration (last registration wins)".
cells: F1 L2 (composition root)
effort: S | route: mechanical sweep (remove + verify `MapAdditionalIdentityEndpoints` has no hidden dependency)
verify: [pending]

### MA-107 | Tier 2 | Bucket A | Slice 1
claim: Two coexisting DI shapes for "one write class serves both CQRS interfaces": eight clusters register the concrete class twice (two instances per scope when both interfaces are injected), while Moderation/Badges use a forwarding delegate whose comment declares instance-unity the point — the codebase itself asserts the distinction matters, so the eight double-registrations either share that requirement (latent bug class) or the delegate is over-engineering. Symmetric — direction undetermined.
evidence: `TheCanalaveLibrary.Server/Program.cs:376-377` — `builder.Services.AddScoped<IGroupReadService, ServerGroupWriteService>(); builder.Services.AddScoped<IGroupWriteService, ServerGroupWriteService>();` (same shape: Series :379-380, StoryLineage :382-383, StoryArc :385-386, SavedTagSelection :278-279, CustomList :281-282, BlogPost :366-367, Notification :402-403) vs. `Program.cs:416-417` — `builder.Services.AddScoped<IModerationWriteService, ServerModerationWriteService>(); builder.Services.AddScoped<IModerationReadService>(sp => sp.GetRequiredService<IModerationWriteService>());` with comment "Forwarding delegate ensures one instance per scope when either interface is injected." (Badges :428-429 same).
cells: F27–F43 L2 (registration shape spans many features; no single cell) 
effort: S | route: seam — direction undetermined (unify on one shape; forwarding delegate is the safer default)
verify: [pending]

### MA-108 | Tier 3 | Bucket A | Slice 1
claim: `SiteBadges` constants live as a top-level class in `Server/Data/SiteConstants.cs` under a stale scaffold comment, diverging from both layer2-services.md's stated shape ("fields on `SiteConstants.SiteBadges`" — no such nesting exists) and the vertical rule (badge keys belong to the Badges cluster).
evidence: `TheCanalaveLibrary.Server/Data/SiteConstants.cs:1,7` — "// In a new file, e.g., Data/SiteConstants.cs" / `public static class SiteBadges` ; `layer2-services.md` §"Synchronous Inline Badge Awards" — "Keys are `public const string` fields on `SiteConstants.SiteBadges`."
cells: F50 L2 organization (doc-vs-code; H-11)
effort: S | route: doc-touch decision (fix the doc's nesting claim) + mechanical move to `Server/Badges/` when next touched
verify: [pending]

### MA-109 | Tier 3 | Bucket C | Slice 1
claim: DataSeeder's authoritative header inventory is drifted: it declares "44 tags (20 characters, …)" but the code seeds 21 characters (20 + the Bulbasaur sprite fixture) = 45 tags; the "Counters mirror the seeded content" claim is also only approximate (e.g. `WordsWritten = 400` is hand-set, not the computed `CountWords` sum).
evidence: `TheCanalaveLibrary.Server/Data/DataSeeder.cs:39` — "44 tags (20 characters, 8 settings, 12 genres, 4 content warnings);" vs. `:155-171` — the 20-name array plus `characters.Add(new Tag { TagName = "Bulbasaur", … });` ; `:760` — `StoriesWritten = 5, WordsWritten = 400, …`.
cells: dev-only seeder (no grid cell)
effort: S | route: mechanical sweep (correct the header counts)
verify: [pending]

### MA-110 | Tier 2 | Bucket A | Slice 1
claim: The production error page (`UseExceptionHandler("/Error")` target) is unmodified template debris that instructs *end users* to enable the Development environment — inappropriate, off-brand content on the one page real users see when the server faults, and outside the ratified role system.
evidence: `TheCanalaveLibrary.Server/Components/Error.razor:17-25` — "<h3>Development Mode</h3> <p>Swapping to <strong>Development</strong> environment will display more detailed information … enable the <strong>Development</strong> environment by setting the <strong>ASPNETCORE_ENVIRONMENT</strong> environment variable…" ; `Program.cs:476` — `app.UseExceptionHandler("/Error", createScopeForErrors: true);` (non-dev branch).
cells: F1 L4 area / error-handling surface (status.md Global Conditions "Error-handling strategy live" never touched this page)
effort: S | route: mechanical sweep (rewrite as a user-facing fault page per NotFound.razor's role treatment)
verify: [pending]

### MA-111 | Tier 3 | Bucket C | Slice 1
claim: `Client/RedirectToLogin.razor` is dead code — referenced nowhere (`Routes.razor`'s `AuthorizeRouteView` declares no `<NotAuthorized>` content), a template leftover.
evidence: `TheCanalaveLibrary.Client/RedirectToLogin.razor:1-10` (whole file); repo-wide grep `RedirectToLogin` in `*.razor` — zero references; `TheCanalaveLibrary.Client/Routes.razor:22` — `<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(DeviceLayout)" />` (no NotAuthorized fragment).
cells: F1 L5 (dead file — though note: deleting vs. wiring it into `<NotAuthorized>` is a small UX decision, since default not-authorized behavior currently shows bare text)
effort: S | route: mechanical sweep
verify: [pending]

### MA-112 | Tier 3 | Bucket A | Slice 1
claim: Legacy technical-layer folder census (this slice owns them) — remaining stragglers and dispositions: `Server/Services/` {`UserDeletionService` → `Server/Identity/` (also pre-primary-ctor idiom), `ServerDeviceDetectionService` → a render/layout cluster}; `Client/Services/` {`WasmDeviceDetectionService` → same cluster}; `Core/ServiceInterfaces/` {`IDeviceDetectionService` → same}; `Core/Models/` {6 files, all `public partial` scaffold debris, all for unbuilt features: `AcknowledgmentRole`,`BetaReader`,`CoAuthor`,`StoryAcknowledgment` → future Collaboration/Stories cluster; `FeatureContribution` → F56; `UserCustomFilter` → `Core/Discovery/`}; `SharedUI/Pages/` {`NotFound.razor` (live page) → `Errors/` or `Home/`}; `Server/Pages/` → delete (MA-105). Per SKILL.md the move happens with the next touching WU — recorded here so no straggler is invisible.
evidence: SKILL.md §"Code Organization" — "Legacy technical-layer folders are deprecated: `Core/Models/` … `Server/Services/` and `Client/Services/` … No new file is ever added to one. Any work-unit that touches a file still living in one of them moves it into its feature cluster"; file paths per the inventory table above (e.g. `TheCanalaveLibrary.Core/Models/UserCustomFilter.cs:5` — `public partial class UserCustomFilter`).
cells: cross-cutting organization (no stage change proposed — the convention itself defers moves to touching WUs)
effort: M (aggregate) | route: mechanical sweep (or per-WU as convention dictates)
verify: [pending]

### MA-113 | Tier 3 | Bucket C | Slice 1
claim: Dead dependency + stale pointer pair around the deferred Redis seam: `Aspire.StackExchange.Redis.DistributedCaching` is referenced by Server but nothing consumes it (the comment says re-add the registration later — the package could ride with it), and AppHost.cs cites `layer7-redis.md`, a convention file dissolved 2026-07-06.
evidence: `TheCanalaveLibrary.Server/TheCanalaveLibrary.Server.csproj` — `<PackageReference Include="Aspire.StackExchange.Redis.DistributedCaching" Version="13.4.6" />` ; `Server/Program.cs:13-14` — "// Redis (write-behind cache, Layer 7) is post-MVP — no feature reads/writes IDistributedCache yet. // Re-add builder.AddRedisDistributedCache("cache") when…" ; `AppHost/AppHost.cs:33-34` — "// Resource name "cache" is the connection-string name the L7 client registration will consume (builder.AddRedisDistributedCache("cache") - see Server/Program.cs comment, layer7-redis.md)."
cells: infra (no cell). The `cache` *container* is a documented deliberate seam (horizontal-scaling.md) — only the unused package + dead doc pointer are flagged.
effort: S | route: mechanical sweep
verify: [pending]

### MA-114 | Tier 3 | Bucket A | Slice 1
claim: Doc-pointer staleness bundle (H-11 class — five instances where a convention doc or XML doc contradicts current code): (a) cross-cutting.md places DevDiagnosticsEndpoints in `Server/Endpoints/` — it lives in `Server/Diagnostics/`; (b) security.md's CSP spec says `font-src 'self'` — code emits `font-src 'self' data:`; (c) testing.md's Unit-tier examples cite `ServerSpriteReadService (fake IWebHostEnvironment)` — that type no longer exists (superseded by Core's `OptimisticSpriteReadService`, no env dep); (d) layer2-services.md §Discovery Defaults says the filter-key map's "keys live in `SiteConstants.cs`" — they moved to `Core/Discovery/SiteSearchModes.cs` (WU28, per SiteConstants.cs's own note); (e) `WriteRateLimitExceededException`'s XML doc promises "429 … with a `Retry-After` header" — EndpointHelpers deliberately uses a body extension, no header (matching security.md).
evidence: (a) `cross-cutting.md` §"Dev-Only Diagnostic Endpoints" — "**Home:** `TheCanalaveLibrary.Server/Endpoints/DevDiagnosticsEndpoints.cs`" vs. actual path `Server/Diagnostics/DevDiagnosticsEndpoints.cs`; (b) `security.md` — "img-src 'self' data:; font-src 'self';" vs. `Server/Security/CspPolicy.cs:25` — `"font-src 'self' data:; "`; (c) `testing.md` tier table — "ServerSpriteReadService (fake `IWebHostEnvironment`)"; (d) `layer2-services.md` — "keys live in `SiteConstants.cs`" vs. `Server/Data/SiteConstants.cs:2` — "// SiteSearchModes and UserStoryInteractionFilters moved to Core/Discovery/SiteSearchModes.cs (WU28)"; (e) `Core/Security/WriteRateLimitExceededException.cs:7-9` — "endpoints translate this to <c>429 Too Many Requests</c> with a <c>Retry-After</c> header" vs. `Server/Http/EndpointHelpers.cs:50-52` — "RetryAfter surfaces in the body (extensions) rather than a response header".
cells: doc-only (Bucket B/doc pass input)
effort: S | route: doc-touch (batch)
verify: [pending]

### MA-115 | Tier 3 | Bucket A | Slice 1
claim: DevDiagnosticsEndpoints' "throwaway, removed once confirmed" banner is stale (H-07): the `/dev/wu12/*` endpoints survived a month and one (`/dev/wu12/login-as`) is now load-bearing for DevLoginBar — the comment (and the WU12-era route names) describe a disposal plan that was superseded; testing.md says kept dev endpoints should carry a deliberate-keep note.
evidence: `TheCanalaveLibrary.Server/Diagnostics/DevDiagnosticsEndpoints.cs:23` — "// --- WU12 verification — throwaway, removed once confirmed (plan: "removed after") ---" ; `:36` — the login-as endpoint whose comment documents DevLoginBar's live dependency ("DevLoginBar renders plain <a> links to this"); `testing.md` — "Don't remove a standing dev-diagnostics endpoint … without checking the relevant audit file for a note that they were deliberately kept."
cells: dev-only surface (no cell)
effort: S | route: mechanical sweep (re-comment as kept; optionally rename routes off "wu12")
verify: [pending]

### MA-116 | Tier 3 | Bucket A | Slice 1
claim: `TODO(user): flesh out the full SearchMode × UserStoryInteractionFilterType default matrix when desired` contradicts layer2-services.md's settled claim that the seed is final ("Seed is authoritative and unchanged … No migration") — one of the two statements is stale (H-07/H-11, symmetric).
evidence: `TheCanalaveLibrary.Server/Data/Configurations/DiscoveryConfigurations.cs:81` — "// TODO(user): flesh out the full SearchMode × UserStoryInteractionFilterType default matrix when desired." vs. `layer2-services.md` §"§8.7 Discovery Defaults" — "**Seed is authoritative and unchanged** (Ignored=true on the 5 discovery surfaces; profiles=none). No migration."
cells: F15/F31 L1 seed (doc-vs-code)
effort: S | route: doc-touch decision (delete the TODO or soften the doc)
verify: [pending]

### MA-117 | Tier 3 | Bucket C | Slice 1
claim: ModelEnums carries SQL-Server-era comment debris — a mangled double comment block citing "TINYINT" and "identity(1,1)" (neither exists under Postgres/EF), and `RecommendationStatusEnum` is 1-indexed against the "0-indexed" enum convention with only in-code (not SKILL-registered) justification — SKILL lists SiteRoles as the sole sanctioned exception.
evidence: `TheCanalaveLibrary.Core/Lookups/ModelEnums.cs:47-52` — "// This enum is a C# "mirror" of the 'StoryStatuses' table. … // We use 'short' to match the 'TINYINT' SQL data type. // This enum is a C# "mirror" of the 'recommendation_statuses' table. … // Note: 1-indexed (the table uses identity(1,1))." ; SKILL.md Naming — "`: short`, 0-indexed … **Exception — SiteRoles:** uses `: int` … and is 1-indexed".
cells: F27 L1 (cosmetic)
effort: S | route: mechanical sweep (fix comments; register the 1-index exception in SKILL or renumber pre-launch)
verify: [pending]

### MA-118 | Tier 3 | Bucket A | Slice 1
claim: The WU42 StoryRelationship→StoryLineage rename stopped at type level: member identifiers (`StoryLineage.RelationshipTypeId`, nav `RelationshipType`, `StoryLineageType`'s PK `RelationshipTypeId`) still carry "Relationship", contradicting layer2-services.md's "neither name contains 'Relationship' anymore" and preserving part of the grep collision the rename existed to remove. Symmetric — code catch-up or doc narrowing.
evidence: `TheCanalaveLibrary.Server/Data/Configurations/StoryConfigurations.cs:283` — `builder.HasKey(e => new { e.SourceStoryId, e.TargetStoryId, e.RelationshipTypeId });` and `:292-294` — `.WithOne(sr => sr.RelationshipType).HasForeignKey(sr => sr.RelationshipTypeId)` ; `layer2-services.md` §"Table naming" — "the near-collision this table originally worked around no longer exists at the identifier level (neither name contains "Relationship" anymore)".
cells: F10 L1 (proposes no reopen — cosmetic; migration would rename columns)
effort: M | route: doc-touch decision (narrow the doc claim) or mechanical rename pre-launch
verify: [pending]

### MA-119 | Tier 3 | Bucket A | Slice 1
claim: The two `IActiveUserContext` test fakes disagree on the `Theme` contract — Integration's fake carries the display name `"Pokémon"` where the interface documents a URL-safe slug (`"pokemon"`, which the Unit-tier stub correctly uses); additionally `IntegrationTestBase.SetActiveUser` copies only 5 of the fake's 7 properties (Theme/PrefersAnimatedSprites silently dropped), so a test setting them would pass garbage without noticing. Symmetric within the test tier.
evidence: `TheCanalaveLibrary.Tests.Integration/FakeActiveUserContext.cs:17` — `public string Theme { get; set; } = "Pokémon";` vs. `Core/Identity/IActiveUserContext.cs:20` — "string Theme { get; }  // URL-safe theme SLUG (e.g. "pokemon")" and `Tests.Unit/ImageTestSupport.cs` StubActiveUserContext — `public string Theme { get; set; } = "pokemon";` ; `Tests.Integration/IntegrationTestBase.cs:118-125` — SetActiveUser copies `UserId/IsAuthenticated/ShowMatureContent/IsModerator/IsAdmin` only.
cells: test infra (no cell)
effort: S | route: mechanical sweep
verify: [pending]

### MA-120 | Tier 3 | Bucket C | Slice 1
claim: PaginationControlsTests contains a tautological loop assertion that verifies nothing (the same always-true selector runs 7 times with the loop variable unused), and its class doc still describes the pre-Phase-A active style (`--color-primary`, "text-white") that the test body itself contradicts by asserting `bg-(--color-action)`.
evidence: `TheCanalaveLibrary.Tests.RazorComponents/PaginationControlsTests.cs:66-70` — `for (int page = 1; page <= 7; page++) { cut.Find($"button[aria-current], button:not([aria-current])").Should().NotBeNull(); }` ; class doc — "the active-state class string ("text-white")" and "(<c>--color-primary</c>, …)" vs. `:198-201` — `activeClass.Should().Contain("bg-(--color-action)", …)`.
cells: F8-area test quality (no cell change)
effort: S | route: mechanical sweep
verify: [pending]

### MA-121 | Tier 3 | Bucket C | Slice 1
claim: Two foundation atoms have no direct tests where testing.md's criteria say they qualify: `ConfirmDialog` (EventCallback logic — backdrop-click ⇒ Cancel, both paths auto-close + `IsOpenChanged(false)` — is exactly the "EventCallback invocations with correct argument values" tier target) has no ConfirmDialogTests; `ClientHttpHelpers`' empty-body→null mapping (the Global-Flip crash class) has no dedicated Unit pin — it is `internal` with no InternalsVisibleTo (a deliberate repo stance), so coverage exists only diffusely through per-feature client-service tests.
evidence: Tests.RazorComponents directory listing — no `ConfirmDialogTests.cs` (calibration.md seam table documents ConfirmDialog's contract: "Backdrop click = Cancel; both paths auto-close + `IsOpenChanged(false)`") ; `TheCanalaveLibrary.Client/Http/ClientHttpHelpers.cs:16` — `internal static class ClientHttpHelpers` ; testing.md §"What belongs in RazorComponents" — "EventCallback invocations with the correct argument values".
cells: Dialogs atom / Http cluster test coverage
effort: S | route: mechanical sweep (add ConfirmDialogTests; pin the helpers through any one public client service)
verify: [pending]

### MA-122 | Tier 3 | Bucket A | Slice 1
claim: `ImageEndpoints` returns bare body-less `Results.NotFound()` against layer5-wasm.md's blanket "Every API error status must be a bodied result" rule — benign here (GET-only, so no 405-re-execute trap) but each missing image re-executes the full `/not-found` Razor page per the middleware, and the rule as written admits no image-serving carve-out. Symmetric: rule scope vs. code.
evidence: `TheCanalaveLibrary.Server/Images/ImageEndpoints.cs:28-30,37-39,50-52` — three `return Results.NotFound();` sites ; `layer5-wasm.md` §"The Error-Translation Contract" — "**Every API error status must be a bodied result (`Results.Problem`), never a bare `Results.NotFound()`…**".
cells: F2-area L5 (Images serving)
effort: S | route: doc-touch decision (scope the rule to API endpoints, or make these bodied)
verify: [pending]

### MA-123 | Tier 3 | Bucket A | Slice 1
claim: Two `RequireModerator()` implementations diverge on the thrown type — SiteSettings throws `UnauthorizedAccessException` (HTTP 403 via EndpointHelpers) while Moderation throws `InvalidOperationException` (HTTP 401) for the same non-mod-caller condition, despite layer2-services.md describing SiteSettings as following "the `ServerModerationWriteService` pattern". Symmetric divergence; S7 owns the other side.
evidence: `TheCanalaveLibrary.Server/SiteSettings/ServerSiteSettingsWriteService.cs:35-36` — `if (!activeUser.IsModerator && !activeUser.IsAdmin) throw new UnauthorizedAccessException("This operation requires a moderator.");` vs. `TheCanalaveLibrary.Server/Moderation/ServerModerationWriteService.cs:282-283` — `if (!activeUser.IsModerator && !activeUser.IsAdmin) throw new InvalidOperationException("Moderator action requires the Moderator or Admin role.");`.
cells: F55/F46 L2 (cross-slice seam)
effort: S | route: seam — direction undetermined (403 is semantically right for authenticated-but-forbidden; direction for S7/doc pass)
verify: [pending]

---

## Hypothesis results (slice 1)

- **H-01** (`@key` on stateful list children): **clean** — the slice's only loops (DesignGalleryPage `@for` buttons, seeder data) render no stateful components.
- **H-02** (route-param reload discipline): **n/a** — no route-param pages in slice (HomePage `/`, NotFound, DesignGallery are parameterless).
- **H-03** (unnamed HasIndex overwrite): **MA-103** — `UserConfiguration`'s unnamed `HasIndex(NormalizedEmail)` silently merged into Identity's named `EmailIndex` and flipped it unique (the H-03 mechanism, consequence live). The original incident site (USI's seven filtered indexes) is confirmed fixed with names + load-bearing comment (`UserStoryInteractionConfigurations.cs:27-34`); no other same-property-set duplicates found across all 20 config files.
- **H-04** (read-context factory-per-method): **clean** — `ServerThemeReadService`, `ServerSiteSettingsReadService` both factory-per-method; `TestAppFactory` mirrors production's scoped-factory registration; no service in slice holds a `ReadOnlyApplicationDbContext` field.
- **H-05** (dead Tailwind classes): **clean in slice code** (paren-form throughout Home/NotFound/App/DesignGallery). MA-009's open question from S0 is RESOLVED: `scripts/check-design-tokens.ps1:19-23` explicitly sanctions DevLoginBar/DesignGalleryPage/ContentSurface as raw-color exemptions — deliberate, not a checker blind spot. Note: `Error.razor`'s Bootstrap-era `text-danger` coincidentally resolves as the `--color-danger` bare-token utility (see MA-110 for that page's real problem).
- **H-06** (unregistered silent catches): **clean** — no empty/comment-only catch in product slice files (Program.cs seeder catch logs Error; ImageUploadProcessor's `catch { dispose; throw }` rethrows; ImageEndpoints' catch translates to 404; `TestAppFactory.Dispose`'s IOException swallow is commented test-infra cleanup, outside logging.md's Server/Core/SharedUI/Client sweep scope).
- **H-07** (stale TODO/WU comments): **MA-115** (WU12 "throwaway" dev endpoints kept + load-bearing), **MA-116** (`TODO(user)` default-matrix vs. "seed is authoritative").
- **H-08** (Nav.NotFound vs manual): **n/a** — no dispatchers with missing-entity branches in slice; the `/not-found` page itself (SharedUI/Pages) is conformant as the re-execute target.
- **H-09** (dispatcher load parallelism): **n/a** — HomePage performs no data loads.
- **H-10** (pending writes lost on dispose): **n/a** — no debounced/deferred write paths in slice (signal buffers live in S2/S3 clusters).
- **H-11** (doc-vs-code staleness): **MA-104** (default-deny posture never implemented), **MA-108** (SiteConstants nesting), **MA-114** (five-instance bundle: DevDiagnostics home, CSP font-src, ServerSpriteReadService, SiteConstants keys, Retry-After header), **MA-118** (StoryLineage member names). Re-confirmed S0's content-safety.md/BaseBlogPost contradiction with fresh evidence: `ReadOnlyApplicationDbContext.cs:50-56` applies `"IsTakenDown"` to `BaseBlogPost` model-level with a test-backed comment, while content-safety.md §"TPT blog-post exception" says it is not applied model-level.
- **H-12** (fire-and-forget without observation): **clean** — no unawaited task launches in slice product code.
- **H-13** (counter discipline): **n/a** — no counter mutations in slice (DataSeeder sets counters by construction, documented).
- **H-14** (elevated reads annotated): **n/a** — zero `IgnoreQueryFilters` calls in slice files.
- **H-15** (write-path ContentRating bypass): **n/a** — no write-service PK reads through readDb in slice; the write-context-unfiltered design is directly regression-pinned by `ContentRatingFilterTests` (line-51 bug tests).
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — no array/collection params on slice endpoints; SiteSettingsEndpoints' scalar body param carries explicit `[FromBody]`.
- **H-17** (nullable client reads use helpers): **n/a for call sites** (none in slice); the helpers themselves (`ClientHttpHelpers.GetNullableFromJsonAsync`/`ReadNullableFromJsonAsync`) exist and match layer5's contract; direct-test gap noted in MA-121.
- **H-18** (aria-labels on icon-only controls): **clean** — UserCard caret (`aria-label='More options'`), ToastHost dismiss (`aria-label='Dismiss'`), PaginationControls prev/next all covered by their tests; no EditorView-wrapping component in slice lacks labels.
- **H-19** (AuthorizeView-gated DI split): **n/a** — no auth-gated service-injecting components in slice.
- **H-20** (feedback-channel discipline): **clean/n/a** — no forms in slice; NotFound/Home are static; DesignGallery is dev-only demo markup.
