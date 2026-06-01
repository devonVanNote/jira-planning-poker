using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using PlanningPoker.Components;
using PlanningPoker.Models;
using PlanningPoker.Services;
using System.Net.Http.Headers;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ValidateRequiredConfiguration(builder.Configuration);

// Static web assets (framework JS, NuGet-sourced files) are auto-enabled only in Development.
// Explicit call required when running under any non-Production environment name (e.g. UAT).
if (!builder.Environment.IsProduction())
{
    builder.WebHost.UseStaticWebAssets();
}

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Force pure Authorization Code flow. Microsoft.Identity.Web defaults to hybrid (code id_token)
// which requires "ID tokens" to be enabled under implicit grant in the Azure app registration.
// Pure code flow requires a client secret but does not require implicit grant.
builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.ResponseType = OpenIdConnectResponseType.Code;
});

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
builder.Services.AddRazorPages();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddHttpClient<IJiraService, JiraService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Jira:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
        Convert.ToBase64String(Encoding.ASCII.GetBytes(builder.Configuration["Jira:ApiSecret"]!)));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

string[] storyPoints = builder.Configuration.GetSection("Sessions:StoryPoints").Get<string[]>()
    ?? ["1", "2", "3", "5", "8", "13", "☕"];
StoryPoints.Initialize(storyPoints);

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(exceptionHandlerApp =>
        exceptionHandlerApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("An unexpected error occurred.");
        }));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static void ValidateRequiredConfiguration(IConfiguration config)
{
    var missing = new List<string>();

    Check("AzureAd:TenantId");
    Check("AzureAd:ClientId");
    Check("AzureAd:ClientSecret");
    Check("Jira:BaseUrl");
    Check("Jira:ApiSecret");

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            $"The following required configuration values are missing or empty:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing.Select(k => $"  - {k}")));
    }

    void Check(string key)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
            missing.Add(key);
    }
}
