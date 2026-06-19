using Microsoft.JSInterop;
using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Client.Services;

/// <summary>
/// Detects the device type on the client (WebAssembly) by using JS interop.
/// </summary>
public class WasmDeviceDetectionService : IDeviceDetectionService
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isMobile;

    public WasmDeviceDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsMobile()
    {
        // In Wasm, this method might be called before the first render completes.
        // A synchronous JS call is required here to implement the interface correctly.
        // This is one of the few cases where a synchronous JS interop call is acceptable.
        if (_isMobile is null)
        {
            _isMobile = ((IJSInProcessRuntime)_jsRuntime).Invoke<bool>("isMobile");
        }
        return _isMobile.Value;
    }
}