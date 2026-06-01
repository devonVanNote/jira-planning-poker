# Jira Setup

This app connects to Jira Cloud using HTTP Basic Authentication with a personal API token.
It reads issues via JQL search, fetches individual issues by key, and writes story point
estimates back to a custom field after a vote.

---

## Step 1 — Generate a Jira API Token

1. Sign in to your Atlassian account at https://id.atlassian.com/manage-profile/security/api-tokens
2. Click **Create API token**.
3. Give it a label (e.g. `planning-poker`) and click **Create**.
4. Copy the token — you will not be able to see it again.

The token is tied to your Atlassian account. The app will make all Jira requests as you,
so your account must have the permissions described in Step 3.

---

## Step 2 — Configure the App

Open `appsettings.json` (or set environment variables / user secrets) and fill in:

```json
"Jira": {
  "BaseUrl":   "https://yourcompany.atlassian.net/",
  "ApiSecret": "your-email@example.com:your-api-token"
}
```

| Setting      | Value                                                                  |
|--------------|------------------------------------------------------------------------|
| `BaseUrl`    | Your Jira Cloud root URL, **with a trailing slash**                    |
| `ApiSecret`  | Your Atlassian account email, a colon, then the API token you copied   |

The app base64-encodes `ApiSecret` and sends it as an HTTP Basic Authorization header,
which is the format Jira Cloud expects.

> **Do not commit credentials.** Use `dotnet user-secrets` for local development or
> environment variables / a secrets manager in production.
>
> ```
> dotnet user-secrets set "Jira:BaseUrl"   "https://yourcompany.atlassian.net/"
> dotnet user-secrets set "Jira:ApiSecret" "your-email@example.com:your-api-token"
> ```

---

## Step 3 — Required Jira Permissions

The Atlassian account whose API token is used must have the following project-level
permissions in every Jira project the app will read from:

| Permission        | Why it is needed                                                   |
|-------------------|--------------------------------------------------------------------|
| **Browse Projects** | Read issues, fields, assignees, reporters, labels, sprint data   |
| **Edit Issues**     | Write the story points estimate back after a vote concludes      |

These are standard Jira project permissions. A project admin can grant them under
**Project settings → People** (role-based) or **Project settings → Permissions**.

---

## Step 4 — Custom Fields

The app reads and writes story points through a Jira custom field. The default field ID is
`customfield_10031`, which is the standard **Story Points** field on most Jira Cloud
instances. If your project uses a different field, update `appsettings.json`:

```json
"Jira": {
  "StoryPointsField":          "customfield_10031",
  "StoryPointsFieldAlternate": ""
}
```

Set `StoryPointsFieldAlternate` if your project stores story points in two fields and both
need to be updated simultaneously (e.g. a legacy field alongside a newer one).

To find your field's ID:
1. Open any issue in Jira.
2. Press `?` → **Keyboard shortcuts**, or navigate to
   `https://yourcompany.atlassian.net/rest/api/3/field` while signed in.
3. Search the JSON response for `storyPoints` or the display name of your field.
   The `id` value is what goes in `StoryPointsField`.

The sprint field (`customfield_10020`) and team field (`Team[Team]`) rarely need to change,
but they follow the same lookup process if they do.

---

## Step 5 — Verify Connectivity

Run the app. If the Jira settings are missing or blank, it will refuse to start and print
the names of every missing key. If the credentials are wrong or the URL is unreachable,
the issue list page will show a Jira API error with the HTTP status code returned.

Common errors:

| Symptom                                         | Likely cause                                              |
|-------------------------------------------------|-----------------------------------------------------------|
| App won't start, lists missing keys             | `BaseUrl` or `ApiSecret` is blank in config               |
| HTTP 401 on the issues page                     | Wrong email or API token in `ApiSecret`                   |
| HTTP 403 on the issues page                     | Account lacks **Browse Projects** on that project         |
| HTTP 403 when saving a vote                     | Account lacks **Edit Issues** on that project             |
| App returns HTML instead of JSON error          | `BaseUrl` is wrong — Jira returned a login redirect page  |
| Sprint or story-points column always empty      | `StoryPointsField` or `SprintField` ID does not match     |
