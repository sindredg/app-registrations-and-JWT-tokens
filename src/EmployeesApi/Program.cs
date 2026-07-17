using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

// Keep Entra claim names as-is ("roles" stays "roles", not a mapped URI).
JsonWebTokenHandler.DefaultMapInboundClaims = false;

// Helper: read every role value regardless of how the claim was named.
static IEnumerable<string> RolesOf(ClaimsPrincipal user) =>
    user.FindAll("roles").Select(c => c.Value)
        .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value));

var builder = WebApplication.CreateBuilder(args);

// Listen on a fixed port so the test commands are predictable.
builder.WebHost.UseUrls("http://localhost:5080");

// Startup: trust tokens issued by YOUR tenant for THIS API's audience.
// Values come from appsettings.json (TenantId / ClientId / Audience).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// Best effort: make IsInRole()/RequireRole() read "roles" too.
builder.Services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options => options.TokenValidationParameters.RoleClaimType = "roles");

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// DELEGATED path - the HR web app acting on behalf of a signed-in user.
// Requires BOTH: the delegated scope (scp) AND the app role (roles).
//   Sindre Writer  -> scp=Employees.Read.All + roles=Survey.Create -> 200
//   Sindre Plain   -> scp but NO role                              -> 403
// ---------------------------------------------------------------------------
app.MapPost("/surveys", (HttpContext ctx) =>
{
    // 1) enforce the scope - read the "scp" claim (space-separated)
    var scopes = ctx.User.FindFirst("scp")?.Value?.Split(' ') ?? Array.Empty<string>();
    if (!scopes.Contains("Employees.Read.All"))
        return Results.Forbid();

    // 2) enforce the app role - read the roles claim
    if (!RolesOf(ctx.User).Contains("Survey.Create"))
        return Results.Forbid();

    return Results.Ok(new
    {
        message = "Survey created",
        actingAs = ctx.User.Identity?.Name ?? "(user)",
        proof = "scp had Employees.Read.All AND roles had Survey.Create"
    });
})
.RequireAuthorization();

// ---------------------------------------------------------------------------
// APPLICATION (app-only) path - the payroll daemon, no user present.
//   Employees Daemon token -> roles=app-Employees.Read.All -> 200
//   Any delegated user token (no that role)                -> 403
// ---------------------------------------------------------------------------
app.MapGet("/employees/all", (HttpContext ctx) =>
{
    if (!RolesOf(ctx.User).Contains("app-Employees.Read.All"))
        return Results.Forbid();

    return Results.Ok(new
    {
        message = "All employee records",
        proof = "roles had app-Employees.Read.All (no user, no scp)"
    });
})
.RequireAuthorization();

app.Run();
