using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheCanalaveLibrary.Client.Services;
using TheCanalaveLibrary.Core.ServiceInterfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

//Client side service registration for dependency injection
builder.Services.AddScoped<IDeviceDetectionService, WasmDeviceDetectionService>();
builder.Services.AddScoped<IStoryOverviewService, HttpStoryOverviewService>();

// Register HttpClient for dependency injection into services like HttpStoryOverviewService
// The base address is configured to point to the server application.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();