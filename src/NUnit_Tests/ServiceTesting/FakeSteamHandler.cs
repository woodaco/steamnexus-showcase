using System.Net;
using System.Reflection;

namespace NUnit_Tests.ServiceTesting;

/// <summary>
/// An HttpMessageHandler that intercepts Steam API calls and returns canned JSON responses
/// based on the request URL path. All real network traffic is blocked.
/// </summary>
public class FakeSteamHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (int StatusCode, string Content)> _routes;

    public FakeSteamHandler()
    {
        _routes = new Dictionary<string, (int, string)>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Register a canned response for a given URL path prefix.
    /// The first matching prefix wins (insertion order).
    /// </summary>
    public void Register(string urlPrefix, int statusCode, string jsonContent)
    {
        _routes[urlPrefix] = (statusCode, jsonContent);
    }

    /// <summary>
    /// Convenience: register a 200 response from an embedded resource.
    /// Fixture files are embedded as NUnit_Tests.ServiceTesting.Fixtures.&lt;filename&gt;.json
    /// </summary>
    public void RegisterFromEmbedded(string urlPrefix, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"NUnit_Tests.ServiceTesting.Fixtures.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found. Check csproj Embed resource config.");

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        _routes[urlPrefix] = ((int)HttpStatusCode.OK, content);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri?.ToString() ?? "";

        foreach (var (prefix, response) in _routes)
        {
            if (uri.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage((HttpStatusCode)response.StatusCode)
                {
                    Content = new StringContent(response.Content, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }

        // Default: 404 for unregistered endpoints (catches misrouted calls)
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"no route\"}", System.Text.Encoding.UTF8, "application/json")
        };
    }
}
