using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

// Global Flip (layer5-wasm.md flip checklist step 3 — the client-registration sweep's findings):
// IActiveUserContext's WASM twin reads the deserialized auth state's claims (SerializeAllClaims
// carries the canalave:* claims across); the IHostEnvironment adapter unblocks DevLoginBar's
// IsDevelopment() gate; ManualTreeStore mirrors the Server's own scoped registration (pure
// IJSRuntime, runtime-agnostic).
builder.Services.AddScoped<IActiveUserContext, WasmActiveUserContext>();
builder.Services.AddScoped<Microsoft.Extensions.Hosting.IHostEnvironment, WasmHostEnvironmentAdapter>();
builder.Services.AddScoped<ManualTreeStore>();

//Client side service registration for dependency injection
// Error-handling UX seams (WU-ErrorHandling) — same pair as the Server host, so ToastHost and
// DraftAutosave resolve identically after the L5 WASM flip.
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<DraftStore>();
builder.Services.AddScoped<IDeviceDetectionService, WasmDeviceDetectionService>();
// OptimisticSpriteReadService is stateless; base URL uses same wwwroot default as Server.
// Both sides share the Core impl — see audit/Sprites.md L5 and layer2-services.md §"Sprite URLs Are Resolved At Render Time."
builder.Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
// Public URL resolution (Seo/, WU-Seo) — Client uses the browser's own origin (NavigationManager
// isn't available yet at registration time, so resolve it lazily via the factory overload).
// Crawlers never reach the Client host (they only ever see the Server prerender), so exactness
// here only matters for already-interactive re-renders — see audit/Seo.md.
builder.Services.AddScoped<IPublicUrlProvider>(sp =>
    new PublicUrlProvider(sp.GetRequiredService<NavigationManager>().BaseUri));
// Tags (L5 WASM pilot) — HttpClient impls over Server/Tags/TagEndpoints.cs. First minted client
// service pair; the pattern (endpoint + Client{Feature}Service + register here) is layer5-wasm.md's.
builder.Services.AddScoped<ITagReadService, ClientTagReadService>();
builder.Services.AddScoped<ITagWriteService, ClientTagWriteService>();

// WU-L5Sweep (2026-07-13): mechanical add-only Layer-5 batch — every remaining ServerXXXService's
// client impl, registered so the codebase is ready for the future InteractiveAuto flip. See
// layer5-wasm.md "Rollout Strategy" — pages still ride the global InteractiveServer mode today, so
// these registrations are inert until the Global Flip; add-without-verify, per-feature browser
// verification is future work.
builder.Services.AddScoped<IStoryReadService, ClientStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, ClientStoryWriteService>();
builder.Services.AddScoped<IStoryArcReadService, ClientStoryArcReadService>();
builder.Services.AddScoped<IStoryArcWriteService, ClientStoryArcWriteService>();
builder.Services.AddScoped<IStoryLineageReadService, ClientStoryLineageReadService>();
builder.Services.AddScoped<IStoryLineageWriteService, ClientStoryLineageWriteService>();
builder.Services.AddScoped<IViewCountWriteService, ClientViewCountWriteService>();
builder.Services.AddScoped<ISeriesReadService, ClientSeriesReadService>();
builder.Services.AddScoped<ISeriesWriteService, ClientSeriesWriteService>();
builder.Services.AddScoped<IChapterReadService, ClientChapterReadService>();
builder.Services.AddScoped<IChapterWriteService, ClientChapterWriteService>();
builder.Services.AddScoped<IChapterReadMarkWriteService, ClientChapterReadMarkWriteService>();
builder.Services.AddScoped<IReadingProgressWriteService, ClientReadingProgressWriteService>();
builder.Services.AddScoped<ICommentReadService, ClientCommentReadService>();
builder.Services.AddScoped<ICommentWriteService, ClientCommentWriteService>();
builder.Services.AddScoped<IUserStoryInteractionReadService, ClientUserStoryInteractionReadService>();
builder.Services.AddScoped<IUserStoryInteractionWriteService, ClientUserStoryInteractionWriteService>();
builder.Services.AddScoped<ISavedTagSelectionReadService, ClientSavedTagSelectionReadService>();
builder.Services.AddScoped<ISavedTagSelectionWriteService, ClientSavedTagSelectionWriteService>();
builder.Services.AddScoped<ICustomListReadService, ClientCustomListReadService>();
builder.Services.AddScoped<ICustomListWriteService, ClientCustomListWriteService>();
builder.Services.AddScoped<IFollowingReadService, ClientFollowingReadService>();
builder.Services.AddScoped<IFollowingWriteService, ClientFollowingWriteService>();
builder.Services.AddScoped<IUserProfileReadService, ClientUserProfileReadService>();
builder.Services.AddScoped<IUserSettingsService, ClientUserSettingsService>();
builder.Services.AddScoped<IThemeReadService, ClientThemeReadService>();
builder.Services.AddScoped<IRecommendationReadService, ClientRecommendationReadService>();
builder.Services.AddScoped<IRecommendationWriteService, ClientRecommendationWriteService>();
builder.Services.AddScoped<IBlogPostReadService, ClientBlogPostReadService>();
builder.Services.AddScoped<IBlogPostWriteService, ClientBlogPostWriteService>();
builder.Services.AddScoped<IPollReadService, ClientPollReadService>();
builder.Services.AddScoped<IPollWriteService, ClientPollWriteService>();
builder.Services.AddScoped<INotificationReadService, ClientNotificationReadService>();
builder.Services.AddScoped<INotificationWriteService, ClientNotificationWriteService>();
builder.Services.AddScoped<IManualTreeSearchReadService, ClientManualTreeSearchReadService>();
builder.Services.AddScoped<ITreeSearchReadService, ClientTreeSearchReadService>();
builder.Services.AddScoped<IDiscoveryDefaultsReadService, ClientDiscoveryDefaultsReadService>();
builder.Services.AddScoped<ICoOccurrenceReadService, ClientCoOccurrenceReadService>();
builder.Services.AddScoped<IGroupReadService, ClientGroupReadService>();
builder.Services.AddScoped<IGroupWriteService, ClientGroupWriteService>();
builder.Services.AddScoped<IModerationReadService, ClientModerationReadService>();
builder.Services.AddScoped<IModerationWriteService, ClientModerationWriteService>();
builder.Services.AddScoped<ISiteDailyStatReadService, ClientSiteDailyStatReadService>();
builder.Services.AddScoped<IMessagingReadService, ClientMessagingReadService>();
builder.Services.AddScoped<IMessagingWriteService, ClientMessagingWriteService>();
builder.Services.AddScoped<ISpotlightReadService, ClientSpotlightReadService>();
builder.Services.AddScoped<ISpotlightWriteService, ClientSpotlightWriteService>();
builder.Services.AddScoped<ISpotlightSlotAllocator, ClientSpotlightSlotAllocator>();
builder.Services.AddScoped<ISiteSettingsReadService, ClientSiteSettingsReadService>();
builder.Services.AddScoped<ISiteSettingsWriteService, ClientSiteSettingsWriteService>();
builder.Services.AddScoped<IBadgeReadService, ClientBadgeReadService>();
builder.Services.AddScoped<IBadgeWriteService, ClientBadgeWriteService>();
builder.Services.AddScoped<IContentImportService, ClientContentImportService>();
builder.Services.AddScoped<IUserActivityWriteService, ClientUserActivityWriteService>();
// Remaining unregistered interfaces are deliberate structural exclusions — never client-implemented
// (server-only infra) or already WASM-native via a shared impl. See layer5-wasm.md "Scope
// Inventory"/"Avoid": IImageStorageService, IHtmlSanitizationService, IWriteRateLimitService,
// IDeviceDetectionService (WasmDeviceDetectionService above), ISpriteReadService
// (OptimisticSpriteReadService above), IExportService (anchor-link download, no client impl).

// Register HttpClient for dependency injection into services
// The base address is configured to point to the server application.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();