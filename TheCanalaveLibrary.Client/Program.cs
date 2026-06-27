using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheCanalaveLibrary.Client;
using TheCanalaveLibrary.Core;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

//Client side service registration for dependency injection
builder.Services.AddScoped<IDeviceDetectionService, WasmDeviceDetectionService>();
// OptimisticSpriteReadService is stateless; base URL uses same wwwroot default as Server.
// Both sides share the Core impl — see audit/Sprites.md L5 and layer2-services.md §"Sprite URLs Are Resolved At Render Time."
builder.Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
// No HTTP story services registered: MVP is InteractiveServer-only (no WASM render mode), so story
// reads/writes resolve to the Server impls directly. The client HTTP impls were divergent dead code
// (called endpoints StoryEndpoints never mapped) — deleted. Re-mint cleanly if WASM L5 is built post-MVP.

// Register HttpClient for dependency injection into services
// The base address is configured to point to the server application.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();