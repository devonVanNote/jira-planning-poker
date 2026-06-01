namespace PlanningPoker.UnitTests;

public class JiraServiceTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    private static JiraService Build(
        Func<HttpRequestMessage, HttpResponseMessage>? handler = null,
        Dictionary<string, string?>? config = null)
    {
        handler ??= _ => JsonOk("""{"issues":[]}""");
        var http = new HttpClient(new FakeHandler(handler))
        {
            BaseAddress = new Uri("https://fake-jira.atlassian.net/")
        };
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build();
        return new JiraService(http, cfg);
    }

    private static HttpResponseMessage JsonOk(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string IssuesJson(params string[] issues) =>
        $$"""{"issues":[{{string.Join(",", issues)}}]}""";

    private static string IssueJson(
        string key = "A-1",
        string summary = "Test Issue",
        string status = "Ready for Sprint",
        string? sprintJson = null,
        string? pointsJson = null,
        string? descriptionJson = null,
        string labelsJson = "[]") =>
        $$"""
        {
            "key": "{{key}}",
            "fields": {
                "summary": "{{summary}}",
                "status": {"name": "{{status}}"},
                "labels": {{labelsJson}},
                "customfield_10031": {{pointsJson ?? "null"}},
                "customfield_10020": {{sprintJson ?? "[]"}},
                "description": {{descriptionJson ?? "null"}}
            }
        }
        """;

    private static string SprintJson(int id, string name) =>
        $$"""[{"name":"{{name}}","id":{{id}}}]""";

    // ── GetEffectiveJql ──────────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveJql_NoTeam_NoConfig_UsesHardcodedDefault()
    {
        var svc = Build();
        string jql = svc.GetEffectiveJql();
        Assert.Contains("issuetype IN (Story, Bug, Task)", jql);
        Assert.Contains("\"Story Points\" IS EMPTY", jql);
    }

    [Fact]
    public void GetEffectiveJql_NoTeam_WithConfig_UsesConfigJql()
    {
        var svc = Build(config: new() { ["Jira:DefaultJql"] = "project = MYPROJ" });
        string jql = svc.GetEffectiveJql();
        Assert.Contains("project = MYPROJ", jql);
        Assert.Contains("\"Story Points\" IS EMPTY", jql);
    }

    [Fact]
    public void GetEffectiveJql_WithTeam_BuildsTeamJql()
    {
        var svc = Build(config: new() { ["Jira:Teams:TeamA"] = "team-guid-123" });
        string jql = svc.GetEffectiveJql("TeamA");
        Assert.Contains("\"Team[Team]\" = \"team-guid-123\"", jql);
        Assert.Contains("\"Story Points\" IS EMPTY", jql);
    }

    [Fact]
    public void GetEffectiveJql_NoPointsFilter_InsertedBeforeOrderBy()
    {
        var svc = Build();
        string jql = svc.GetEffectiveJql();
        int filterIdx = jql.IndexOf("\"Story Points\" IS EMPTY", StringComparison.Ordinal);
        int orderByIdx = jql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        Assert.True(filterIdx >= 0 && orderByIdx >= 0 && filterIdx < orderByIdx);
    }

    [Fact]
    public void GetEffectiveJql_CustomJql_WithJqlParam_AppendedWithNoPointsFilter()
    {
        var svc = Build();
        // GetItemsAsync with jql param also applies the filter
        string jql = svc.GetEffectiveJql();
        Assert.Contains("\"Story Points\" IS EMPTY", jql);
    }

    // ── GetTeamsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamsAsync_ReturnsSortedNames()
    {
        var svc = Build(config: new()
        {
            ["Jira:Teams:Zebra"] = "g1",
            ["Jira:Teams:Alpha"] = "g2",
            ["Jira:Teams:Beta"] = "g3"
        });
        var teams = await svc.GetTeamsAsync();
        Assert.Equal(["Alpha", "Beta", "Zebra"], teams);
    }

    [Fact]
    public async Task GetTeamsAsync_NoConfig_ReturnsEmpty()
    {
        var svc = Build();
        Assert.Empty(await svc.GetTeamsAsync());
    }

    // ── GetItemsAsync ─── parsing ────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_ParsesKeyAndSummary()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(key: "TEST-1", summary: "My Issue"))));
        var items = await svc.GetItemsAsync();
        Assert.Single(items);
        Assert.Equal("TEST-1", items[0].Key);
        Assert.Equal("My Issue", items[0].Summary);
    }

    [Fact]
    public async Task GetItemsAsync_ParsesStatus()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(status: "Approved"))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("Approved", items[0].Status);
    }

    [Fact]
    public async Task GetItemsAsync_ParsesOptionalFields()
    {
        const string json = """
        {
            "issues": [{
                "key": "T-1",
                "fields": {
                    "summary": "Test",
                    "status": {"name": "Ready for Sprint"},
                    "issuetype": {"name": "Story"},
                    "priority": {"name": "High"},
                    "assignee": {"displayName": "John Doe"},
                    "reporter": {"displayName": "Jane Doe"},
                    "labels": ["frontend","backend"],
                    "customfield_10031": null,
                    "customfield_10020": []
                }
            }]
        }
        """;
        var svc = Build(_ => JsonOk(json));
        var items = await svc.GetItemsAsync();
        Assert.Equal("Story", items[0].IssueType);
        Assert.Equal("High", items[0].Priority);
        Assert.Equal("John Doe", items[0].AssigneeName);
        Assert.Equal("Jane Doe", items[0].ReporterName);
        Assert.Equal(["frontend", "backend"], items[0].Labels);
    }

    [Fact]
    public async Task GetItemsAsync_ParsesNumericStoryPoints()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(pointsJson: "5"))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("5", items[0].CurrentPoints);
    }

    [Fact]
    public async Task GetItemsAsync_ParsesDecimalStoryPoints()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(pointsJson: "5.0"))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("5.0", items[0].CurrentPoints);
    }

    [Fact]
    public async Task GetItemsAsync_ParsesSprintInfo()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(sprintJson: SprintJson(42, "Sprint 3")))));
        var items = await svc.GetItemsAsync();
        Assert.Equal(42, items[0].SprintId);
        Assert.Equal("Sprint 3", items[0].SprintName);
    }

    [Fact]
    public async Task GetItemsAsync_EmptyIssues_ReturnsEmpty()
    {
        var svc = Build(_ => JsonOk("""{"issues":[]}"""));
        Assert.Empty(await svc.GetItemsAsync());
    }

    [Fact]
    public async Task GetItemsAsync_NullOptionalFields_DoNotThrow()
    {
        const string json = """
        {
            "issues": [{
                "key": "T-1",
                "fields": {
                    "summary": "Test",
                    "status": {"name": "Ready for Sprint"},
                    "issuetype": null,
                    "priority": null,
                    "assignee": null,
                    "reporter": null,
                    "labels": [],
                    "customfield_10031": null,
                    "customfield_10020": []
                }
            }]
        }
        """;
        var svc = Build(_ => JsonOk(json));
        var items = await svc.GetItemsAsync();
        Assert.Single(items);
        Assert.Null(items[0].IssueType);
        Assert.Null(items[0].Priority);
        Assert.Null(items[0].AssigneeName);
        Assert.Null(items[0].ReporterName);
    }

    // ── GetItemsAsync ─── sorting ────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_SortsByStatusOrder()
    {
        var svc = Build(_ => JsonOk(IssuesJson(
            IssueJson(key: "A-1", status: "Estimation"),
            IssueJson(key: "A-2", status: "Ready for Sprint"),
            IssueJson(key: "A-3", status: "Approved"))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("A-2", items[0].Key); // Ready for Sprint = 0
        Assert.Equal("A-3", items[1].Key); // Approved = 1
        Assert.Equal("A-1", items[2].Key); // Estimation = 2
    }

    [Fact]
    public async Task GetItemsAsync_ItemsWithSprint_BeforeItemsWithout()
    {
        var svc = Build(_ => JsonOk(IssuesJson(
            IssueJson(key: "A-1", status: "Approved"),
            IssueJson(key: "A-2", status: "Approved", sprintJson: SprintJson(10, "Sprint 1")))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("A-2", items[0].Key); // has sprint → first
        Assert.Equal("A-1", items[1].Key); // no sprint → second
    }

    [Fact]
    public async Task GetItemsAsync_SortsBySprintIdAscending()
    {
        var svc = Build(_ => JsonOk(IssuesJson(
            IssueJson(key: "A-1", status: "Approved", sprintJson: SprintJson(50, "Sprint 5")),
            IssueJson(key: "A-2", status: "Approved", sprintJson: SprintJson(10, "Sprint 1")))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("A-2", items[0].Key); // sprint 10 < sprint 50
        Assert.Equal("A-1", items[1].Key);
    }

    // ── GetItemsAsync ─── error handling ────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_HttpError_Throws()
    {
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized", Encoding.UTF8, "text/plain")
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetItemsAsync());
    }

    [Fact]
    public async Task GetItemsAsync_NonJsonContentType_Throws()
    {
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Login</html>", Encoding.UTF8, "text/html")
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetItemsAsync());
    }

    // ── GetItemsAsync ─── ADF description parsing ────────────────────────────

    [Fact]
    public async Task GetItemsAsync_AdfParagraph_ParsedToHtml()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [{"type": "text", "text": "Hello world"}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Equal("<p>Hello world</p>", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfBoldText_ParsedToStrong()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [{
                    "type": "text",
                    "text": "Bold",
                    "marks": [{"type": "strong"}]
                }]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<strong>Bold</strong>", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfEmText_ParsedToEm()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [{"type": "text", "text": "italic", "marks": [{"type": "em"}]}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<em>italic</em>", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfCodeText_ParsedToCode()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [{"type": "text", "text": "snippet", "marks": [{"type": "code"}]}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<code>snippet</code>", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfBulletList_ParsedToUlLi()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "bulletList",
                "content": [{
                    "type": "listItem",
                    "content": [{
                        "type": "paragraph",
                        "content": [{"type": "text", "text": "Item 1"}]
                    }]
                }]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<ul>", items[0].Description);
        Assert.Contains("<li>", items[0].Description);
        Assert.Contains("Item 1", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfOrderedList_ParsedToOl()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "orderedList",
                "content": [{
                    "type": "listItem",
                    "content": [{
                        "type": "paragraph",
                        "content": [{"type": "text", "text": "Step 1"}]
                    }]
                }]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<ol>", items[0].Description);
        Assert.Contains("Step 1", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfHeading_ParsedWithLevel()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "heading",
                "attrs": {"level": 2},
                "content": [{"type": "text", "text": "Title"}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<h2>Title</h2>", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfCodeBlock_ParsedToPreCode()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "codeBlock",
                "content": [{"type": "text", "text": "var x = 1;"}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<pre><code>", items[0].Description);
        Assert.Contains("var x = 1;", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfHardBreak_ParsedToBr()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [
                    {"type": "text", "text": "line1"},
                    {"type": "hardBreak"},
                    {"type": "text", "text": "line2"}
                ]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.Contains("<br>", items[0].Description);
        Assert.Contains("line1", items[0].Description);
        Assert.Contains("line2", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_AdfHtmlEncoding_EscapesSpecialChars()
    {
        const string desc = """
        {
            "type": "doc",
            "content": [{
                "type": "paragraph",
                "content": [{"type": "text", "text": "<script>alert('xss')</script>"}]
            }]
        }
        """;
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: desc))));
        var items = await svc.GetItemsAsync();
        Assert.DoesNotContain("<script>", items[0].Description);
        Assert.Contains("&lt;script&gt;", items[0].Description);
    }

    [Fact]
    public async Task GetItemsAsync_NullDescription_ReturnsNullDescription()
    {
        var svc = Build(_ => JsonOk(IssuesJson(IssueJson(descriptionJson: "null"))));
        var items = await svc.GetItemsAsync();
        Assert.Null(items[0].Description);
    }

    // ── GetItemByKeyAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemByKeyAsync_ReturnsItem()
    {
        const string json = """
        {
            "key": "TEST-1",
            "fields": {
                "summary": "By Key Issue",
                "status": {"name": "Ready for Sprint"},
                "labels": [],
                "customfield_10031": null,
                "customfield_10020": []
            }
        }
        """;
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var item = await svc.GetItemByKeyAsync("TEST-1");
        Assert.NotNull(item);
        Assert.Equal("TEST-1", item.Key);
        Assert.Equal("By Key Issue", item.Summary);
    }

    [Fact]
    public async Task GetItemByKeyAsync_NotFound_ReturnsNull()
    {
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await svc.GetItemByKeyAsync("MISSING-1"));
    }

    [Fact]
    public async Task GetItemByKeyAsync_ServerError_ReturnsNull()
    {
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.Null(await svc.GetItemByKeyAsync("T-1"));
    }

    // ── UpdateStoryPointsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateStoryPointsAsync_SendsPutToCorrectUrl()
    {
        HttpRequestMessage? captured = null;
        var svc = Build(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        await svc.UpdateStoryPointsAsync("TEST-1", 8m);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Contains("TEST-1", captured.RequestUri?.ToString() ?? "");
    }

    [Fact]
    public async Task UpdateStoryPointsAsync_PayloadContainsPoints()
    {
        string? requestBody = null;
        var svc = Build(req =>
        {
            requestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        await svc.UpdateStoryPointsAsync("T-1", 5m);
        Assert.NotNull(requestBody);
        Assert.Contains("5", requestBody);
        Assert.Contains("customfield_10031", requestBody);
    }

    [Fact]
    public async Task UpdateStoryPointsAsync_HttpError_Throws()
    {
        var svc = Build(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad Request", Encoding.UTF8, "text/plain")
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateStoryPointsAsync("TEST-1", 5m));
    }

    // ── GetItemsAsync with custom jql param ──────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_CustomJql_AppendedWithNoPointsFilter()
    {
        string? capturedBody = null;
        var svc = Build(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonOk("""{"issues":[]}""");
        });
        await svc.GetItemsAsync(jql: "project = TEST");
        Assert.NotNull(capturedBody);
        Assert.Contains("Story Points", capturedBody);
    }

    // ── SprintField ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_CustomSprintField_ParsesSprint()
    {
        const string json = """
        {
            "issues": [{
                "key": "A-1",
                "fields": {
                    "summary": "Test",
                    "status": {"name": "Ready for Sprint"},
                    "labels": [],
                    "customfield_10031": null,
                    "my_sprint_field": [{"name": "Sprint 7", "id": 77}],
                    "description": null
                }
            }]
        }
        """;
        var svc = Build(_ => JsonOk(json), config: new() { ["Jira:SprintField"] = "my_sprint_field" });
        var items = await svc.GetItemsAsync();
        Assert.Equal(77, items[0].SprintId);
        Assert.Equal("Sprint 7", items[0].SprintName);
    }

    [Fact]
    public async Task GetItemsAsync_CustomSprintField_IncludedInFieldsRequest()
    {
        string? capturedBody = null;
        var svc = Build(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonOk("""{"issues":[]}""");
        }, config: new() { ["Jira:SprintField"] = "my_sprint_field" });

        await svc.GetItemsAsync();

        Assert.Contains("my_sprint_field", capturedBody);
    }

    // ── TeamField ────────────────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveJql_WithCustomTeamField_UsesConfiguredField()
    {
        var svc = Build(config: new()
        {
            ["Jira:TeamField"] = "Squad[Team]",
            ["Jira:Teams:TeamA"] = "guid-abc"
        });
        string jql = svc.GetEffectiveJql("TeamA");
        Assert.Contains("\"Squad[Team]\" = \"guid-abc\"", jql);
    }

    [Fact]
    public void GetEffectiveJql_WithTeam_DefaultTeamField_UsesTeamTeam()
    {
        var svc = Build(config: new() { ["Jira:Teams:TeamA"] = "guid-abc" });
        string jql = svc.GetEffectiveJql("TeamA");
        Assert.Contains("\"Team[Team]\" = \"guid-abc\"", jql);
    }

    // ── PriorityStatuses ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_CustomPriorityStatuses_SortsByConfiguredOrder()
    {
        var svc = Build(
            _ => JsonOk(IssuesJson(
                IssueJson(key: "A-1", status: "Backlog"),
                IssueJson(key: "A-2", status: "Refined"),
                IssueJson(key: "A-3", status: "In Progress"))),
            config: new()
            {
                ["Jira:PriorityStatuses:0"] = "In Progress",
                ["Jira:PriorityStatuses:1"] = "Refined",
                ["Jira:PriorityStatuses:2"] = "Backlog"
            });

        var items = await svc.GetItemsAsync();

        Assert.Equal("A-3", items[0].Key); // In Progress = 0
        Assert.Equal("A-2", items[1].Key); // Refined = 1
        Assert.Equal("A-1", items[2].Key); // Backlog = 2
    }

    [Fact]
    public async Task GetItemsAsync_StatusNotInPriorityList_SortedLast()
    {
        var svc = Build(
            _ => JsonOk(IssuesJson(
                IssueJson(key: "A-1", status: "Unknown Status"),
                IssueJson(key: "A-2", status: "Ready for Sprint"))),
            config: new()
            {
                ["Jira:PriorityStatuses:0"] = "Ready for Sprint"
            });

        var items = await svc.GetItemsAsync();

        Assert.Equal("A-2", items[0].Key); // known status = 0
        Assert.Equal("A-1", items[1].Key); // unknown = last
    }

    // ── MaxResults ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_DefaultMaxResults_Sends200()
    {
        string? capturedBody = null;
        var svc = Build(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonOk("""{"issues":[]}""");
        });
        await svc.GetItemsAsync();

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal(200, doc.RootElement.GetProperty("maxResults").GetInt32());
    }

    [Fact]
    public async Task GetItemsAsync_CustomMaxResults_SentInRequest()
    {
        string? capturedBody = null;
        var svc = Build(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonOk("""{"issues":[]}""");
        }, config: new() { ["Jira:MaxResults"] = "50" });
        await svc.GetItemsAsync();

        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal(50, doc.RootElement.GetProperty("maxResults").GetInt32());
    }
}
