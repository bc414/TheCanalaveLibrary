using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// Bridges <see cref="IHostEnvironment"/> to WASM's <see cref="IWebAssemblyHostEnvironment"/>
/// (Global Flip — layer5-wasm.md flip checklist step 3's known instance): <c>DevLoginBar</c>
/// gates itself on <c>IHostEnvironment.IsDevelopment()</c>, and WASM DI registers only
/// <see cref="IWebAssemblyHostEnvironment"/>. The WASM host inherits the server's environment
/// name (Blazor sets it from the <c>blazor-environment</c> header/dev server), so
/// <c>IsDevelopment()</c> agrees across both runtimes. File-system members return inert values —
/// nothing reachable in WASM reads them.
/// </summary>
public class WasmHostEnvironmentAdapter(IWebAssemblyHostEnvironment wasmEnvironment) : IHostEnvironment
{
    public string EnvironmentName
    {
        get => wasmEnvironment.Environment;
        set => throw new NotSupportedException("The WASM host environment is read-only.");
    }

    public string ApplicationName { get; set; } = "TheCanalaveLibrary.Client";

    public string ContentRootPath { get; set; } = "/";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
