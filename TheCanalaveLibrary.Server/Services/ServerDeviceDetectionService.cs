using Microsoft.AspNetCore.Http;
using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Server.Services;

/// <summary>
/// Detects the device type on the server by inspecting the User-Agent header.
/// This implementation is for static server-side rendering (SSR) and interactive server components.
/// </summary>
public class ServerDeviceDetectionService : IDeviceDetectionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool? _isMobile;

    public ServerDeviceDetectionService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsMobile()
    {
        if (_isMobile.HasValue)
        {
            return _isMobile.Value;
        }

        string userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "";
        _isMobile = userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase);
        return _isMobile.Value;
    }
}