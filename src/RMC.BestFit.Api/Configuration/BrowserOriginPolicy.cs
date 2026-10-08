using System.Net;

namespace RMC.BestFit.Api.Configuration;

/// <summary>
/// Validates browser origins before requests reach REST controllers or MCP tools.
/// </summary>
/// <remarks>
/// Originless native clients remain supported. Implicit same-origin access is limited to
/// literal loopback authorities to avoid trusting arbitrary Host values during DNS rebinding.
/// Other browser origins require an exact entry in Cors:AllowedOrigins. This is a browser
/// boundary, not authentication: native clients can omit or choose the Origin header.
/// </remarks>
internal sealed class BrowserOriginPolicy
{
    /// <summary>The normalized explicitly configured browser origins.</summary>
    private readonly HashSet<string> _allowedOrigins = new(StringComparer.Ordinal);

    /// <summary>Validates and captures the configured origin allowlist at startup.</summary>
    /// <param name="configuration">The host configuration.</param>
    /// <exception cref="InvalidOperationException">An allowlist entry is not a single HTTP or HTTPS origin.</exception>
    public BrowserOriginPolicy(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");
        if (section.Value is not null)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be an array of explicit HTTP or HTTPS origins.");
        }

        foreach (var entry in section.GetChildren())
        {
            if (entry.GetChildren().Any() || !TryParseOrigin(entry.Value, out var origin))
            {
                throw new InvalidOperationException(
                    $"{entry.Path} must be a single explicit HTTP or HTTPS origin (scheme, host, and optional port; no wildcard, path, credentials, query, or fragment).");
            }

            _allowedOrigins.Add(Normalize(origin!));
        }
    }

    /// <summary>Checks a request before any CORS preflight or endpoint can execute.</summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>True for originless clients, configured origins, or a matching literal loopback origin.</returns>
    public bool IsRequestAllowed(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var values)) return true;
        if (values.Count != 1 || !TryParseOrigin(values[0], out var origin)) return false;
        if (_allowedOrigins.Contains(Normalize(origin!))) return true;

        return IsLoopback(origin!)
            && TryParseOrigin($"{request.Scheme}://{request.Host.Value}", out var requestOrigin)
            && Normalize(origin!) == Normalize(requestOrigin!);
    }

    /// <summary>Checks explicit browser origins for the CORS response policy.</summary>
    /// <param name="origin">The browser's Origin header.</param>
    /// <returns>True only for a valid explicitly configured origin.</returns>
    /// <remarks>Same-origin requests need no cross-origin response permission.</remarks>
    public bool IsConfiguredOriginAllowed(string origin)
    {
        return TryParseOrigin(origin, out var parsed) && _allowedOrigins.Contains(Normalize(parsed!));
    }

    /// <summary>Parses a single serialized HTTP or HTTPS origin without URI path normalization.</summary>
    /// <param name="value">The origin header or configuration entry.</param>
    /// <param name="origin">The parsed URI when valid, otherwise null.</param>
    /// <returns>True only for an origin containing a scheme, host, and optional port.</returns>
    private static bool TryParseOrigin(string? value, out Uri? origin)
    {
        origin = null;
        if (string.IsNullOrEmpty(value)
            || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '\\' or '%' or '*' or ','))
        {
            return false;
        }

        int separator = value.IndexOf("://", StringComparison.Ordinal);
        if (separator < 0) return false;
        string authority = value[(separator + 3)..];
        if (authority.Length == 0 || authority.EndsWith(':') || authority.IndexOfAny(['/', '?', '#', '@']) >= 0)
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out origin)
            && (origin.Scheme == Uri.UriSchemeHttp || origin.Scheme == Uri.UriSchemeHttps)
            && origin.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6
            && origin.IsWellFormedOriginalString();
    }

    /// <summary>Forms a canonical scheme, host, and effective-port identity.</summary>
    /// <param name="origin">A validated origin.</param>
    /// <returns>The canonical identity with default ports omitted.</returns>
    private static string Normalize(Uri origin) => origin.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped).ToLowerInvariant();

    /// <summary>Recognizes localhost and literal loopback IP addresses without DNS resolution.</summary>
    /// <param name="origin">A validated origin.</param>
    /// <returns>True only for localhost or a loopback IP literal.</returns>
    private static bool IsLoopback(Uri origin)
    {
        return origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(origin.Host, out var address) && IPAddress.IsLoopback(address));
    }
}
