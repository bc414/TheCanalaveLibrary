using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

//Client side service registration for dependency injection
// Error-handling UX seams (WU-ErrorHandling) — same pair as the Server host, so ToastHost and
// DraftAutosave resolve identically after the L5 WASM flip.
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<DraftStore>();
builder.Services.AddScoped<IDeviceDetectionService, WasmDeviceDetectionService>();
// OptimisticSpriteReadService is stateless; base URL uses same wwwroot default as Server.
// Both sides share the Core impl — see audit/Sprites.md L5 and layer2-services.md §"Sprite URLs Are Resolved At Render Time."
builder.Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
// Tags (L5 WASM pilot) — HttpClient impls over Server/Tags/TagEndpoints.cs. First minted client
// service pair; the pattern (endpoint + Client{Feature}Service + register here) is layer5-wasm.md's.
builder.Services.AddScoped<ITagReadService, ClientTagReadService>();
builder.Services.AddScoped<ITagWriteService, ClientTagWriteService>();
// Other features have no HTTP impls yet: their pages still render InteractiveServer, so their
// services resolve to the Server impls. Each feature gains its Client pair when its page joins
// the WASM surface (Phase 4 L5 batch — middle_plan.md).

// Register HttpClient for dependency injection into services
// The base address is configured to point to the server application.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();