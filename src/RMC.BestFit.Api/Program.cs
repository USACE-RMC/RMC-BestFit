using System.Text.Json;
using System.Text.Json.Serialization;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Service Configuration
// =============================================================================

// Configure JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();

        // Seatbelt against mid-stream serialization crashes: NaN is a legitimate marker for
        // missing hydrologic observations (e.g., USGS gap fill) and is emitted as the JSON string
        // literal "NaN". ±Infinity is caught earlier by the ResponseFiniteAuditor; this option is
        // the last line of defense. Clients must enable the matching flag on their deserializer.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

// Configure OpenAPI
builder.Services.AddOpenApi();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());

    // More restrictive policy for production
    options.AddPolicy("Production", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "https://localhost" })
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Bind the API limits (resource cap, run throttle, iteration cap)
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

// Register the resource store and the services shared by REST controllers and MCP tools.
// Everything is a singleton: all state lives in the store, and services are stateless facades.
builder.Services.AddSingleton<IResourceStore, InMemoryResourceStore>();
builder.Services.AddSingleton<IUsgsTimeSeriesService, UsgsTimeSeriesService>();
builder.Services.AddSingleton<ITimeSeriesService, TimeSeriesService>();
builder.Services.AddSingleton<IInputDataService, InputDataService>();
builder.Services.AddSingleton<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();

// MCP server: same service layer as the REST controllers, exposed as tools over the streamable
// HTTP transport. Stateless mode is correct here because all state lives in the app-singleton
// resource store, so resource ids remain valid across MCP sessions and the REST/MCP boundary.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<RMC.BestFit.Api.Mcp.MetadataTools>()
    .WithTools<RMC.BestFit.Api.Mcp.TimeSeriesTools>()
    .WithTools<RMC.BestFit.Api.Mcp.InputDataTools>()
    .WithTools<RMC.BestFit.Api.Mcp.AnalysisTools>()
    .WithTools<RMC.BestFit.Api.Mcp.AdvancedAnalysisTools>()
    .WithTools<RMC.BestFit.Api.Mcp.WorkflowTools>();

// Add health checks
builder.Services.AddHealthChecks();

// =============================================================================
// Application Configuration
// =============================================================================

var app = builder.Build();

// Configure error handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Configure OpenAPI endpoint
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.MapOpenApi();
}

// HTTPS redirection is skipped in Development: local MCP clients connect over plain HTTP and a
// redirect would break the streamable HTTP transport.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");
app.UseAuthorization();

// Map controllers
app.MapControllers();

// MCP endpoint (streamable HTTP transport)
app.MapMcp("/mcp");

// =============================================================================
// Health and Info Endpoints
// =============================================================================

// Health check endpoint
app.MapHealthChecks("/health");

// Detailed health check with version info
app.MapGet("/health/detailed", () => Results.Ok(new HealthCheckDto
{
    Status = "healthy",
    Timestamp = DateTime.UtcNow,
    Version = typeof(Program).Assembly.GetName().Version?.ToString()
}))
.WithName("DetailedHealthCheck")
.WithTags("Health")
.Produces<HealthCheckDto>(StatusCodes.Status200OK);

// Service info endpoint describing the exposed feature areas
app.MapGet("/api/info", () => Results.Ok(new ApiInfoDto
{
    Name = "RMC-BestFit API",
    Version = typeof(Program).Assembly.GetName().Version?.ToString(),
    Description = "REST API and MCP server for Bayesian flood-frequency analysis with RMC-BestFit.",
    Features = new List<string> { "timeseries", "inputdata", "analyses", "workflows", "mcp", "metadata", "resources" }
}))
.WithName("ApiInfo")
.WithTags("Info")
.Produces<ApiInfoDto>(StatusCodes.Status200OK);

// Error handler endpoint
app.MapGet("/error", () => Results.Problem(
    title: "An error occurred",
    statusCode: StatusCodes.Status500InternalServerError))
.ExcludeFromDescription();

// =============================================================================
// Run Application
// =============================================================================

app.Run();

/// <summary>
/// Marker partial class making the top-level-statement entry point visible to the integration
/// test host (<c>WebApplicationFactory&lt;Program&gt;</c>).
/// </summary>
public partial class Program { }
