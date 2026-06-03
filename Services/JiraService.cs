using PlanningPoker.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PlanningPoker.Services;

public class JiraService : IJiraService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly string _storyPointsField;
    private readonly string? _storyPointsFieldAlt;
    private readonly string _sprintField;
    private readonly string _teamField;
    private readonly string[] _priorityStatuses;
    private readonly string? _excludedSummaryPattern;
    private readonly int _maxResults;
    private readonly string[] _fieldsArray;

    public JiraService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        _storyPointsField = config["Jira:StoryPointsField"] ?? "customfield_10031";
        _storyPointsFieldAlt = config["Jira:StoryPointsFieldAlternate"];
        _sprintField = config["Jira:SprintField"] ?? "customfield_10020";
        _teamField = config["Jira:TeamField"] ?? "Team[Team]";
        _priorityStatuses = config.GetSection("Jira:PriorityStatuses").Get<string[]>()
            ?? ["Ready for Sprint", "Approved", "Estimation"];
        _excludedSummaryPattern = config["Jira:ExcludedSummaryPattern"] ?? "Regression Testing";
        _maxResults = config.GetValue<int>("Jira:MaxResults", 200);
        _fieldsArray = ["summary", "status", "issuetype", "priority", "assignee", "reporter",
                        "labels", _storyPointsField, _sprintField, "description"];
    }

    public string GetEffectiveJql(string? team = null)
    {
        string summaryFilter = !string.IsNullOrEmpty(_excludedSummaryPattern)
            ? $" AND summary !~ \"{_excludedSummaryPattern}\""
            : "";

        string baseJql;
        if (!string.IsNullOrEmpty(team))
        {
            string teamGuid = _config[$"Jira:Teams:{team}"] ?? "";
            baseJql = $"\"{_teamField}\" = \"{teamGuid}\" AND issuetype IN (Story, Bug, Task){summaryFilter} ORDER BY created DESC";
        }
        else
        {
            string configJql = _config["Jira:DefaultJql"] ?? "";
            baseJql = string.IsNullOrWhiteSpace(configJql)
                ? $"issuetype IN (Story, Bug, Task){summaryFilter} ORDER BY created DESC"
                : configJql;
        }

        return AppendNoPointsFilter(baseJql);
    }

    public async Task<List<JiraItem>> GetItemsAsync(string? team = null, string? jql = null)
    {
        string effectiveJql = jql != null ? AppendNoPointsFilter(jql) : GetEffectiveJql(team);
        List<JiraItem> items = await SearchByJqlAsync(effectiveJql);
        return SortBySprint(items);
    }

    private int StatusOrder(string status)
    {
        int index = Array.IndexOf(_priorityStatuses, status);
        return index >= 0 ? index : _priorityStatuses.Length;
    }

    private List<JiraItem> SortBySprint(List<JiraItem> items) =>
        [.. items
            .OrderBy(i => StatusOrder(i.Status))
            .ThenBy(i => i.SprintId.HasValue ? 0 : 1)
            .ThenBy(i => i.SprintId ?? int.MaxValue)];

    private static string AppendNoPointsFilter(string jql)
    {
        const string filter = "\"Story Points\" IS EMPTY";
        int orderByIdx = jql.IndexOf(" ORDER BY", StringComparison.OrdinalIgnoreCase);
        return orderByIdx >= 0
            ? $"{jql[..orderByIdx]} AND {filter}{jql[orderByIdx..]}"
            : $"{jql} AND {filter}";
    }

    private async Task<List<JiraItem>> SearchByJqlAsync(string jql)
    {
        string body = JsonSerializer.Serialize(new
        {
            jql,
            maxResults = _maxResults,
            fields = _fieldsArray
        });

        HttpResponseMessage response = await _http.PostAsync("rest/api/3/search/jql",
            new StringContent(body, Encoding.UTF8, "application/json"));
        string content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"JIRA returned HTTP {(int)response.StatusCode}: {content.Trim()}");
        }

        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"JIRA returned '{contentType}' instead of JSON — verify BaseUrl and ApiSecret are correct. " +
                $"BaseUrl: {_http.BaseAddress}");
        }

        using JsonDocument doc = JsonDocument.Parse(content);
        return ParseIssues(doc.RootElement);
    }

    private List<JiraItem> ParseIssues(JsonElement root)
    {
        List<JiraItem> items = [];

        if (!root.TryGetProperty("issues", out JsonElement issuesEl))
        {
            return items;
        }

        foreach (JsonElement issue in issuesEl.EnumerateArray())
        {
            JiraItem? item = ParseSingleIssue(issue);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private JiraItem? ParseSingleIssue(JsonElement issue)
    {
        if (!issue.TryGetProperty("fields", out JsonElement f))
        {
            return null;
        }

        JiraItem item = new JiraItem
        {
            Key = issue.GetProperty("key").GetString() ?? "",
            Summary = f.GetProperty("summary").GetString() ?? "",
            Status = f.GetProperty("status").GetProperty("name").GetString() ?? ""
        };

        if (f.TryGetProperty("issuetype", out JsonElement issuetype) && issuetype.ValueKind != JsonValueKind.Null)
        {
            item.IssueType = issuetype.TryGetProperty("name", out JsonElement itn) ? itn.GetString() : null;
        }

        if (f.TryGetProperty("priority", out JsonElement priority) && priority.ValueKind != JsonValueKind.Null)
        {
            item.Priority = priority.TryGetProperty("name", out JsonElement pn) ? pn.GetString() : null;
        }

        if (f.TryGetProperty("assignee", out JsonElement assignee) && assignee.ValueKind != JsonValueKind.Null)
        {
            item.AssigneeName = assignee.TryGetProperty("displayName", out JsonElement dn) ? dn.GetString() : null;
        }

        if (f.TryGetProperty("reporter", out JsonElement reporter) && reporter.ValueKind != JsonValueKind.Null)
        {
            item.ReporterName = reporter.TryGetProperty("displayName", out JsonElement rn) ? rn.GetString() : null;
        }

        if (f.TryGetProperty("labels", out JsonElement labels) && labels.ValueKind == JsonValueKind.Array)
        {
            item.Labels = [.. labels.EnumerateArray().Select(l => l.GetString()).Where(l => l != null).Cast<string>()];
        }

        if (f.TryGetProperty(_storyPointsField, out JsonElement sp) && sp.ValueKind != JsonValueKind.Null)
        {
            item.CurrentPoints = sp.ValueKind == JsonValueKind.Number ? sp.GetDecimal().ToString() : sp.GetString();
        }

        if (f.TryGetProperty(_sprintField, out JsonElement sprints) && sprints.ValueKind == JsonValueKind.Array)
        {
            JsonElement first = sprints.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                if (first.TryGetProperty("name", out JsonElement sn))
                {
                    item.SprintName = sn.GetString();
                }

                if (first.TryGetProperty("id", out JsonElement si) && si.ValueKind == JsonValueKind.Number)
                {
                    item.SprintId = si.GetInt32();
                }
            }
        }

        if (f.TryGetProperty("description", out JsonElement desc) && desc.ValueKind == JsonValueKind.Object)
        {
            item.Description = ExtractAdfHtml(desc);
        }

        return item;
    }

    public async Task<JiraItem?> GetItemByKeyAsync(string key)
    {
        string fieldsParam = string.Join(",", _fieldsArray);
        HttpResponseMessage response = await _http.GetAsync($"rest/api/3/issue/{WebUtility.UrlEncode(key)}?fields={fieldsParam}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string content = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(content);
        return ParseSingleIssue(doc.RootElement);
    }

    private static string? ExtractAdfHtml(JsonElement node)
    {
        StringBuilder sb = new StringBuilder();
        AppendHtmlNodes(node, sb);
        string html = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(html) ? null : html;
    }

    private static void AppendHtmlNodes(JsonElement node, StringBuilder sb)
    {
        string? typeName = node.TryGetProperty("type", out JsonElement typeEl) ? typeEl.GetString() : null;

        if (typeName == "hardBreak")
        {
            sb.Append("<br>");
            return;
        }

        if (typeName == "text")
        {
            string raw = node.TryGetProperty("text", out JsonElement t) ? t.GetString() ?? "" : "";
            string encoded = WebUtility.HtmlEncode(raw);

            if (node.TryGetProperty("marks", out JsonElement marks) && marks.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement mark in marks.EnumerateArray())
                {
                    string? markType = mark.TryGetProperty("type", out JsonElement mt) ? mt.GetString() : null;
                    encoded = markType switch
                    {
                        "strong" => $"<strong>{encoded}</strong>",
                        "em" => $"<em>{encoded}</em>",
                        "code" => $"<code>{encoded}</code>",
                        "strike" => $"<s>{encoded}</s>",
                        "underline" => $"<u>{encoded}</u>",
                        _ => encoded
                    };
                }
            }

            sb.Append(encoded);
            return;
        }

        int headingLevel = 3;
        if (typeName == "heading" && node.TryGetProperty("attrs", out JsonElement attrs) && attrs.TryGetProperty("level", out JsonElement lvl))
        {
            headingLevel = Math.Clamp(lvl.GetInt32(), 1, 6);
        }

        string? openTag = typeName switch
        {
            "paragraph" => "<p>",
            "heading" => $"<h{headingLevel}>",
            "bulletList" => "<ul>",
            "orderedList" => "<ol>",
            "listItem" => "<li>",
            "blockquote" => "<blockquote>",
            "codeBlock" => "<pre><code>",
            _ => null
        };

        string? closeTag = typeName switch
        {
            "paragraph" => "</p>",
            "heading" => $"</h{headingLevel}>",
            "bulletList" => "</ul>",
            "orderedList" => "</ol>",
            "listItem" => "</li>",
            "blockquote" => "</blockquote>",
            "codeBlock" => "</code></pre>",
            _ => null
        };

        if (openTag != null)
        {
            sb.Append(openTag);
        }

        if (node.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in content.EnumerateArray())
            {
                AppendHtmlNodes(child, sb);
            }
        }

        if (closeTag != null)
        {
            sb.Append(closeTag);
        }
    }

    public Task<List<string>> GetTeamsAsync()
    {
        List<string> teams = [.. _config.GetSection("Jira:Teams").GetChildren().Select(c => c.Key).OrderBy(k => k)];
        return Task.FromResult(teams);
    }

    public async Task UpdateStoryPointsAsync(string issueKey, decimal points)
    {
        double value = (double)points;
        Dictionary<string, object> fields = new() { [_storyPointsField] = value };
        if (!string.IsNullOrEmpty(_storyPointsFieldAlt))
        {
            fields[_storyPointsFieldAlt] = value;
        }

        string payload = JsonSerializer.Serialize(new { fields });
        StringContent content = new StringContent(payload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _http.PutAsync($"rest/api/3/issue/{issueKey}", content);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"JIRA returned HTTP {(int)response.StatusCode} updating {issueKey}: {body.Trim()}");
        }
    }
}
