using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Integration;

/// <summary>
/// Exercises browser-origin enforcement through the real REST and MCP HTTP pipelines.
/// Each host owns an isolated resource store; no network downloads or estimators are used.
/// </summary>
[TestClass]
public class BrowserOriginHttpTests
{
    /// <summary>A valid manual observation request shared by REST and MCP fixtures.</summary>
    private const string ManualInput = "{\"name\":\"origin-test\",\"exactData\":[{\"index\":2000,\"value\":100}]}";

    /// <summary>Checks that hostile pages cannot read resource contents in either environment.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development")]
    [DataRow("Production")]
    public async Task UntrustedOrigin_CannotReadResources(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        await CreateInputAsync(client);
        using var request = CreateRequest(HttpMethod.Get, "/api/resources", "https://example.invalid");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("origin-test", StringComparison.Ordinal));
    }

    /// <summary>Checks that a denied JSON write cannot create a resource before CORS rejects access.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development")]
    [DataRow("Production")]
    public async Task UntrustedOrigin_CannotCreateRestResource(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var request = CreateRequest(HttpMethod.Post, "/api/inputdata/manual", "https://example.invalid");
        request.Content = JsonContent(ManualInput);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(0, factory.Services.GetRequiredService<IResourceStore>().TotalCount,
            "An untrusted browser request must not reach the resource-creating endpoint.");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>Checks that browser simple requests are denied before content-type handling.</summary>
    /// <param name="contentType">A browser-safelisted content type that needs no preflight.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("application/x-www-form-urlencoded")]
    [DataRow("multipart/form-data")]
    [DataRow("text/plain")]
    public async Task UntrustedSimplePost_IsForbidden(string contentType)
    {
        using var factory = CreateFactory("Development");
        using var client = CreateClient(factory);
        using var request = CreateRequest(HttpMethod.Post, "/api/inputdata/manual", "https://example.invalid");
        request.Content = new StringContent(ManualInput, Encoding.UTF8, contentType);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(0, factory.Services.GetRequiredService<IResourceStore>().TotalCount);
    }

    /// <summary>Checks that the shared guard also prevents MCP tools from modifying resources.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development")]
    [DataRow("Production")]
    public async Task UntrustedOrigin_CannotCallMcpTool(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var request = CreateMcpRequest("https://example.invalid",
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"create_inputdata_manual\",\"arguments\":" + ManualInput + "}}");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(0, factory.Services.GetRequiredService<IResourceStore>().TotalCount);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Checks that denied preflights never advertise permission for a POST or MCP headers.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development")]
    [DataRow("Production")]
    public async Task UntrustedPreflight_IsForbidden(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var request = CreatePreflight("https://example.invalid");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Methods"));
    }

    /// <summary>Checks strict origin syntax and exact origin matching instead of suffix/prefix matching.</summary>
    /// <param name="origin">An invalid or untrusted origin header.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("null")]
    [DataRow("*")]
    [DataRow("https://trusted.example/")]
    [DataRow("https://trusted.example/path")]
    [DataRow("https://trusted.example?query")]
    [DataRow("https://trusted.example#fragment")]
    [DataRow("https://user@trusted.example")]
    [DataRow("https://trusted.example https://other.example")]
    [DataRow("https://trusted.example,https://other.example")]
    [DataRow("https://trusted.example\\path")]
    [DataRow("https://trusted.example:")]
    [DataRow("https://trusted.example:65536")]
    [DataRow("https://trusted.example.evil.invalid")]
    [DataRow("https://sub.trusted.example")]
    [DataRow("http://trusted.example")]
    [DataRow("https://trusted.example:444")]
    [DataRow("file://trusted.example")]
    [DataRow("not-an-origin")]
    public async Task InvalidOrDifferentOrigin_IsForbidden(string origin)
    {
        using var factory = CreateFactory("Development", "https://trusted.example");
        using var client = CreateClient(factory);
        using var request = CreateRequest(HttpMethod.Get, "/api/resources", origin);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>Checks that even two individually trusted Origin values are rejected as ambiguous.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    public async Task MultipleOriginHeaders_AreForbidden()
    {
        using var factory = CreateFactory("Development", "https://trusted.example");
        using var client = CreateClient(factory);
        using var request = CreateRequest(HttpMethod.Get, "/api/resources", "https://trusted.example");
        request.Headers.TryAddWithoutValidation("Origin", "https://trusted.example");

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Checks an explicitly empty header without HttpClient dropping its empty value.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    public async Task EmptyOriginHeader_IsForbidden()
    {
        using var factory = CreateFactory("Development");
        var response = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = "GET";
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/api/resources";
            context.Request.Headers.Origin = "";
        });

        Assert.AreEqual(StatusCodes.Status403Forbidden, response.Response.StatusCode);
    }

    /// <summary>Checks that native originless clients can create/read data and discover MCP tools.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development")]
    [DataRow("Production")]
    public async Task OriginlessClients_CanCreateReadAndDiscoverMcp(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        await CreateInputAsync(client);
        using var overview = await client.GetAsync("/api/resources");
        Assert.AreEqual(HttpStatusCode.OK, overview.StatusCode);
        StringAssert.Contains(await overview.Content.ReadAsStringAsync(), "origin-test");

        using var request = CreateMcpRequest(null, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "create_inputdata_manual");
    }

    /// <summary>Checks same-origin browser writes with case/default-port normalization.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <param name="address">The server address.</param>
    /// <param name="origin">The equivalent browser origin.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development", "http://localhost:5158", "http://LOCALHOST:5158")]
    [DataRow("Development", "http://localhost", "HTTP://LOCALHOST:80")]
    [DataRow("Production", "https://localhost", "HTTPS://LOCALHOST:443")]
    [DataRow("Production", "https://127.0.0.1:8443", "https://127.0.0.1:8443")]
    [DataRow("Development", "http://[::1]:5158", "http://[::1]:5158")]
    public async Task SameOriginBrowser_CanCreateResource(string environment, string address, string origin)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory, address);
        client.DefaultRequestHeaders.Add("Origin", origin);
        await CreateInputAsync(client);
    }

    /// <summary>Checks that a hostile matching Host does not turn a foreign origin into a trusted local origin.</summary>
    /// <param name="address">An address that is not a literal loopback authority.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("http://evil.invalid")]
    [DataRow("http://localhost.evil.invalid")]
    [DataRow("http://notlocalhost")]
    [DataRow("http://192.168.1.10")]
    public async Task ForeignMatchingHost_IsForbiddenUnlessConfigured(string address)
    {
        using (var factory = CreateFactory("Development"))
        using (var client = CreateClient(factory, address))
        using (var request = CreateRequest(HttpMethod.Get, "/api/resources", address))
        using (var response = await client.SendAsync(request))
        {
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var configuredFactory = CreateFactory("Development", address);
        using var configuredClient = CreateClient(configuredFactory, address);
        configuredClient.DefaultRequestHeaders.Add("Origin", address);
        await CreateInputAsync(configuredClient);
    }

    /// <summary>Checks explicit cross-origin browser access and MCP protocol request headers.</summary>
    /// <param name="environment">The ASP.NET Core environment.</param>
    /// <param name="configured">The configured trusted origin.</param>
    /// <param name="origin">An equivalent request origin.</param>
    /// <returns>The asynchronous test operation.</returns>
    [TestMethod]
    [DataRow("Development", "https://trusted.example", "https://trusted.example")]
    [DataRow("Production", "https://trusted.example", "https://trusted.example")]
    [DataRow("Development", "HTTPS://TRUSTED.EXAMPLE:443", "https://trusted.example")]
    [DataRow("Production", "https://trusted.example", "HTTPS://TRUSTED.EXAMPLE:443")]
    public async Task ConfiguredBrowser_CanPreflightCreateAndDiscoverMcp(string environment, string configured, string origin)
    {
        using var factory = CreateFactory(environment, configured);
        using var client = CreateClient(factory);
        using var preflight = CreatePreflight(origin);
        using var preflightResponse = await client.SendAsync(preflight);
        Assert.AreEqual(HttpStatusCode.NoContent, preflightResponse.StatusCode);
        Assert.AreEqual(origin, preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        string headers = string.Join(",", preflightResponse.Headers.GetValues("Access-Control-Allow-Headers"));
        StringAssert.Contains(headers.ToLowerInvariant(), "mcp-protocol-version");
        StringAssert.Contains(headers.ToLowerInvariant(), "mcp-session-id");

        client.DefaultRequestHeaders.Add("Origin", origin);
        await CreateInputAsync(client);
        using var request = CreateMcpRequest(null, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "create_inputdata_manual");
    }

    /// <summary>Checks startup fails closed for invalid configured origins in every environment.</summary>
    /// <param name="configured">The invalid configuration entry.</param>
    [TestMethod]
    [DataRow("*")]
    [DataRow("null")]
    [DataRow("")]
    [DataRow("https://*.example")]
    [DataRow("https://trusted.example/")]
    [DataRow("https://trusted.example/path")]
    [DataRow("https://trusted.example?query")]
    [DataRow("https://trusted.example#fragment")]
    [DataRow("https://user@trusted.example")]
    [DataRow("https://trusted.example https://other.example")]
    [DataRow("https://trusted.example:")]
    [DataRow("ftp://trusted.example")]
    [DataRow("not-an-origin")]
    public void InvalidOriginConfiguration_PreventsStartup(string configured)
    {
        foreach (string environment in new[] { "Development", "Production" })
        {
            using var factory = CreateFactory(environment, configured);
            var exception = Assert.ThrowsException<InvalidOperationException>(() => CreateClient(factory));
            StringAssert.Contains(exception.Message, "Cors:AllowedOrigins");
        }
    }

    /// <summary>Creates an isolated host with an optional explicit origin configuration.</summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="configuredOrigin">The allowlist override, or null to use application defaults.</param>
    /// <returns>The configured test factory.</returns>
    private static WebApplicationFactory<Program> CreateFactory(string environment, string? configuredOrigin = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (configuredOrigin is not null)
            {
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = configuredOrigin }));
            }
        });
    }

    /// <summary>Creates a nonredirecting client using HTTPS unless a test specifies another origin.</summary>
    /// <param name="factory">The isolated application factory.</param>
    /// <param name="address">The simulated request address.</param>
    /// <returns>The HTTP client.</returns>
    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string address = "https://localhost")
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(address), AllowAutoRedirect = false });
    }

    /// <summary>Creates and reads back a resource without invoking any numerical estimator.</summary>
    /// <param name="client">The client with the origin headers being tested.</param>
    /// <returns>The asynchronous assertion operation.</returns>
    private static async Task CreateInputAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/inputdata/manual", JsonContent(ManualInput));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<InputDataResourceResponse>(TestJson.Options);
        Assert.IsNotNull(created?.InputData);
        using var detail = await client.GetAsync($"/api/inputdata/{created.InputData.Id}?includeData=true");
        Assert.AreEqual(HttpStatusCode.OK, detail.StatusCode);
        StringAssert.Contains(await detail.Content.ReadAsStringAsync(), "origin-test");
    }

    /// <summary>Creates an HTTP request preserving malformed origin values for negative tests.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The route path.</param>
    /// <param name="origin">The origin value, or null to omit the header.</param>
    /// <returns>The disposable request.</returns>
    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? origin)
    {
        var request = new HttpRequestMessage(method, path);
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        return request;
    }

    /// <summary>Creates a CORS preflight carrying MCP transport headers.</summary>
    /// <param name="origin">The browser origin.</param>
    /// <returns>The disposable preflight request.</returns>
    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = CreateRequest(HttpMethod.Options, "/mcp", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,mcp-protocol-version,mcp-session-id");
        return request;
    }

    /// <summary>Creates a valid streamable HTTP MCP request.</summary>
    /// <param name="origin">The browser origin, or null to omit it.</param>
    /// <param name="body">The JSON-RPC payload.</param>
    /// <returns>The disposable MCP request.</returns>
    private static HttpRequestMessage CreateMcpRequest(string? origin, string body)
    {
        var request = CreateRequest(HttpMethod.Post, "/mcp", origin);
        request.Content = JsonContent(body);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        return request;
    }

    /// <summary>Encodes a JSON request body.</summary>
    /// <param name="body">The JSON payload.</param>
    /// <returns>The disposable HTTP content.</returns>
    private static StringContent JsonContent(string body) => new(body, Encoding.UTF8, "application/json");
}
