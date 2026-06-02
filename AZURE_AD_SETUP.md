# Auth Setup Guide

This application ships with Microsoft Entra ID (Azure AD) as its auth provider, but it is built on standard ASP.NET Core OpenID Connect middleware and can be switched to any OIDC-compliant provider (Okta, Auth0, Google, Keycloak, etc.) with a small set of targeted changes. The Azure-specific sections below cover the default setup; [Switching to a different OIDC provider](#switching-to-a-different-oidc-provider) at the end of this file covers what to change.

---

## Azure Entra ID Setup

You need two app registrations in your Azure tenant — one for the **Server** (the web app itself) and one for the **Client** (the Blazor WASM front-end). If your tenant allows it, a single registration with both a client secret and SPA redirect URIs can work, but two separate registrations are the standard and more secure approach.

---

## Prerequisites

- An Azure subscription with an Entra ID (Azure AD) tenant.
- Permission to create App Registrations (Application Developer role or higher).

---

## Step 1 — Register the Server (API) Application

1. Go to **Azure Portal → Entra ID → App registrations → New registration**.
2. Set the **Name** (e.g. `Planning Poker API`).
3. For **Supported account types**, choose based on your audience:
   - *Single tenant* — only users in your organization.
   - *Multitenant* — any Azure AD organization.
4. Set the **Redirect URI** to **Web** and enter:
   ```
   https://<your-domain>/signin-oidc
   ```
   For local development also add:
   ```
   https://localhost:<port>/signin-oidc
   ```
5. Click **Register**.

### 1a — Add a Client Secret

1. In the registration, go to **Certificates & secrets → New client secret**.
2. Give it a description and expiry, then click **Add**.
3. **Copy the secret value immediately** — it is only shown once.

### 1b — Expose an API (so the Client can call it)

1. Go to **Expose an API → Add a scope**.
2. If prompted, accept the default **Application ID URI** (e.g. `api://<server-client-id>`).
3. Add a scope named `access_as_user`:
   - **Who can consent**: Admins and users.
   - **Admin consent display name**: Access Planning Poker as user.
4. Click **Add scope**.

### 1c — Record these values

| Value | Where to find it |
|---|---|
| Tenant ID | Overview → Directory (tenant) ID |
| Server Client ID | Overview → Application (client) ID |
| Client Secret | The value you copied in Step 1a |
| API Scope | `api://<server-client-id>/access_as_user` |

---

## Step 2 — Register the Client (SPA) Application

1. Go to **App registrations → New registration**.
2. Set the **Name** (e.g. `Planning Poker Client`).
3. For **Supported account types**, match your choice from Step 1.
4. Set the **Redirect URI** to **Single-page application (SPA)** and enter:
   ```
   https://<your-domain>/authentication/login-callback
   ```
   For local development also add:
   ```
   https://localhost:<port>/authentication/login-callback
   ```
5. Click **Register**.

### 2a — Grant API permission

1. Go to **API permissions → Add a permission → My APIs**.
2. Select the **Server** registration from Step 1.
3. Check the `access_as_user` scope and click **Add permissions**.
4. If required by your tenant policy, click **Grant admin consent**.

### 2b — Record these values

| Value | Where to find it |
|---|---|
| Client Client ID | Overview → Application (client) ID |

---

## Step 3 — Configure the Server application

Edit `Server/appsettings.json` (or supply values via environment variables / user secrets in development):

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "<your-tenant-domain>.onmicrosoft.com",
  "TenantId": "<tenant-id>",
  "ClientId": "<server-client-id>",
  "ClientSecret": "<client-secret>",
  "CallbackPath": "/signin-oidc"
}
```

> **Never commit `ClientSecret` to source control.** Use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) locally and environment variables or a secrets manager (Azure Key Vault, etc.) in production.
>
> To store the secret locally:
> ```bash
> dotnet user-secrets set "AzureAd:ClientSecret" "<your-secret>" --project Server
> ```

---

## Step 4 — Configure the Client application

Edit `Client/wwwroot/appsettings.json`:

```json
"AzureAd": {
  "Authority": "https://login.microsoftonline.com/<tenant-id>",
  "ClientId": "<client-client-id>",
  "ValidateAuthority": true,
  "Scopes": "api://<server-client-id>/access_as_user"
}
```

> This file is served publicly to browsers. Do **not** put secrets here — only the client ID and scope are needed.

---

## Step 5 — Configure Jira

Edit `Server/appsettings.json`:

```json
"Jira": {
  "BaseUrl": "https://<your-domain>.atlassian.net/",
  "ApiSecret": "<email>:<api-token>",
  "StoryPointsField": "customfield_10031",
  "StoryPointsFieldAlternate": "",
  "SprintField": "customfield_10020",
  "TeamField": "Team[Team]",
  "PriorityStatuses": [ "Ready for Sprint", "Approved", "Estimation" ],
  "MaxResults": 200,
  "DefaultJql": "",
  "Teams": {
    "Team Name": "<jira-team-id>"
  }
}
```

### Finding your Story Points field ID

Different Jira instances use different custom field IDs for Story Points. To find yours:

1. Open any issue in Jira and add Story Points if not already set.
2. Go to **Jira Settings → Issues → Custom fields**, find "Story Points", and note its ID.

   Alternatively, call the Jira API:
   ```
   GET https://<your-domain>.atlassian.net/rest/api/3/field
   ```
   Look for a field with `name: "Story Points"` and use its `id` value.

3. Common values: `customfield_10016` or `customfield_10031`. Try `customfield_10016` first if the default doesn't work.

If your instance requires writing to two fields on point updates (some configurations sync Story Points to a secondary field), set `StoryPointsFieldAlternate` to the second field ID.

### Finding your Sprint field ID

The sprint field is also a Jira custom field that varies by instance. Use the same API call above and look for a field with `name: "Sprint"`. The default `customfield_10020` is correct for most Jira Cloud instances.

### Configuring the Team field name

`TeamField` is the Jira field name used in JQL when filtering by team (e.g. `"Team[Team]" = "<guid>"`). The display name of this field in JQL depends on how your Jira instance was configured. If team filtering produces no results, check your Jira board configuration or ask your Jira admin for the correct field name.

### Configuring Priority Statuses

`PriorityStatuses` controls how backlog items are sorted in the session creation wizard. Items whose status matches the first entry appear at the top; items not in the list appear at the bottom. Set this to the ordered list of statuses your workflow uses for refinement-ready work.

### Max Results

`MaxResults` controls how many issues are fetched per Jira query. Increase it if your backlog is large; decrease it for faster load times on smaller boards. Jira Cloud caps this at 100 per page for some endpoints — if you hit that limit, consider narrowing your query with `DefaultJql` instead.

### Creating a Jira API Token

1. Go to [id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens).
2. Click **Create API token** and give it a label.
3. Set `ApiSecret` to `<your-email>:<api-token>`.

### Team IDs (optional)

If you want team-specific boards in the session creation wizard, populate `Jira:Teams` with team names and their Jira Team field GUIDs. Leave the section empty (`{}`) to use a single shared board driven by `DefaultJql`.

---

## Step 6 — Configure Session Storage

Sessions and user preferences are stored as JSON files on disk. Set the path to a location **outside your application directory** so that sessions persist across deployments:

```json
"Sessions": {
  "StoragePath": "/var/data/planningpoker"
}
```

On Windows: `"C:\\PlanningPokerData"` or similar. On Linux/containers: an absolute path on a mounted persistent volume.

> The path defaults to `App_Data/sessions` relative to the working directory, which is suitable for development but will be lost on a full redeploy.

---

## Environment Variable Reference

All configuration keys can be supplied as environment variables using `__` (double underscore) as the section separator:

| Setting | Environment variable |
|---|---|
| `AzureAd:TenantId` | `AzureAd__TenantId` |
| `AzureAd:ClientId` | `AzureAd__ClientId` |
| `AzureAd:ClientSecret` | `AzureAd__ClientSecret` |
| `Jira:BaseUrl` | `Jira__BaseUrl` |
| `Jira:ApiSecret` | `Jira__ApiSecret` |
| `Jira:StoryPointsField` | `Jira__StoryPointsField` |
| `Jira:StoryPointsFieldAlternate` | `Jira__StoryPointsFieldAlternate` |
| `Jira:SprintField` | `Jira__SprintField` |
| `Jira:TeamField` | `Jira__TeamField` |
| `Jira:MaxResults` | `Jira__MaxResults` |
| `Sessions:StoragePath` | `Sessions__StoragePath` |

Environment variables take precedence over `appsettings.json`, making them the recommended approach for production secrets and per-environment overrides.

---

## Switching to a different OIDC provider

The Azure integration is confined to four places. The changes below replace it with generic ASP.NET Core OpenID Connect middleware, which works with any OIDC-compliant provider.

### 1 — NuGet packages

Remove the two Microsoft-specific packages:

```
dotnet remove package Microsoft.Identity.Web
dotnet remove package Microsoft.Identity.Web.UI
```

Add the standard cookie package if it is not already present (it ships with the ASP.NET Core meta-package on .NET 8+, so this step may be a no-op):

```
dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect
```

---

### 2 — Program.cs

Replace the Microsoft Identity block with standard OIDC middleware. The diff is:

**Remove:**
```csharp
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.ResponseType = OpenIdConnectResponseType.Code;
});

builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
```

**Add:**
```csharp
using Microsoft.AspNetCore.Authentication.Cookies;

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority    = builder.Configuration["Oidc:Authority"];
    options.ClientId     = builder.Configuration["Oidc:ClientId"];
    options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.CallbackPath = "/signin-oidc";
    options.SaveTokens   = true;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
});

builder.Services.AddControllersWithViews();
```

Also add an `AccountController` to handle sign-in and sign-out (create `Controllers/AccountController.cs`):

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

[Route("[controller]/[action]")]
public class AccountController : Controller
{
    [HttpGet]
    public IActionResult SignIn(string returnUrl = "/") =>
        Challenge(new AuthenticationProperties { RedirectUri = returnUrl },
                  OpenIdConnectDefaults.AuthenticationScheme);

    [HttpGet]
    public async Task<IActionResult> SignOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" });
        return Redirect("/");
    }
}
```

Update `ValidateRequiredConfiguration` at the bottom of `Program.cs` to check the new keys:

```csharp
// Replace AzureAd checks with:
Check("Oidc:Authority");
Check("Oidc:ClientId");
Check("Oidc:ClientSecret");
```

---

### 3 — appsettings.json

Replace the `AzureAd` section with an `Oidc` section:

```json
"Oidc": {
  "Authority":    "https://<provider-domain>/",
  "ClientId":     "",
  "ClientSecret": ""
}
```

The `Authority` value is provider-specific — see the table below.

---

### 4 — Razor components

Two components hardcode the Microsoft Identity sign-in/sign-out URLs and need updating.

**`Components/Layout/LoginDisplay.razor`** — change the href and the `NavigateTo` call:

```razor
@inject NavigationManager Navigation

<AuthorizeView>
    <Authorized>
        <span class="login-display text-white-50 small me-2">@context.User.Identity?.Name</span>
        <button class="btn btn-sm btn-outline-light" @onclick="SignOut">Sign out</button>
    </Authorized>
    <NotAuthorized>
        <a href="Account/SignIn" class="btn btn-sm btn-outline-light">Sign in</a>
    </NotAuthorized>
</AuthorizeView>

@code {
    private void SignOut() =>
        Navigation.NavigateTo("Account/SignOut", forceLoad: true);
}
```

**`Components/Layout/RedirectToLogin.razor`** — update the redirect target:

```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized() =>
        Navigation.NavigateTo(
            $"Account/SignIn?returnUrl={Uri.EscapeDataString(Navigation.Uri)}",
            forceLoad: true);
}
```

---

### 5 — Provider-specific Authority values

| Provider | Authority value |
|----------|-----------------|
| **Okta** | `https://<your-okta-domain>/oauth2/default` |
| **Auth0** | `https://<your-tenant>.auth0.com/` |
| **Google** | `https://accounts.google.com` |
| **Keycloak** | `https://<host>/realms/<realm>` |
| **Azure Entra ID** (original) | `https://login.microsoftonline.com/<tenant-id>/v2.0` |

Most providers also require you to register a redirect URI in their dashboard — use the same value you would have used for Azure: `https://<your-domain>/signin-oidc` (and `https://localhost:<port>/signin-oidc` for local dev).
