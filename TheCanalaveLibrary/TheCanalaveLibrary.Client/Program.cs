using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TheCanalaveLibrary.Client.Services;
using TheCanalaveLibrary.Core.ServiceInterfaces;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

//Client side service registration for dependency injection
builder.Services.AddScoped<IDeviceDetectionService, WasmDeviceDetectionService>();

await builder.Build().RunAsync();