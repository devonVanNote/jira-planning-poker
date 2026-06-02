using PlanningPoker.Models;

namespace PlanningPoker.Services;

public class DemoJiraService : IJiraService
{
    private static readonly List<JiraItem> Items =
    [
        new()
        {
            Key = "DEMO-101", Summary = "Implement user preference persistence across devices",
            Status = "Ready for Sprint", IssueType = "Story", Priority = "High",
            AssigneeName = "Alice Johnson", ReporterName = "Carol Williams",
            SprintName = "Sprint 42", SprintId = 42,
            Description = "<p>Users expect their theme, language, and notification settings to follow them when they switch devices. Preferences are currently stored in localStorage and lost on every new browser/device.</p><p>Store preferences server-side, keyed by user identity, and sync on login.</p>"
        },
        new()
        {
            Key = "DEMO-102", Summary = "Session countdown timer resets unexpectedly on reconnect",
            Status = "Ready for Sprint", IssueType = "Bug", Priority = "High",
            AssigneeName = "Bob Smith", ReporterName = "Dave Brown",
            SprintName = "Sprint 42", SprintId = 42,
            Labels = ["regression", "reliability"],
            Description = "<p>When a participant loses connection and reconnects mid-session, the countdown timer shown to them resets to its initial value rather than reflecting the actual remaining time.</p><p><strong>Steps to reproduce:</strong></p><ol><li>Start a session with a 60-second timer.</li><li>Wait 30 seconds.</li><li>Disconnect and immediately reconnect a participant.</li><li>Observe the timer showing 60 seconds instead of ~30.</li></ol>"
        },
        new()
        {
            Key = "DEMO-103", Summary = "Add export to CSV for completed sprint velocity",
            Status = "Approved", IssueType = "Story", Priority = "Medium",
            AssigneeName = "Carol Williams", ReporterName = "Alice Johnson",
            SprintName = "Sprint 42", SprintId = 42,
        },
        new()
        {
            Key = "DEMO-104", Summary = "Upgrade authentication library to v4",
            Status = "Approved", IssueType = "Task", Priority = "Medium",
            AssigneeName = "Alice Johnson", ReporterName = "Alice Johnson",
            SprintName = "Sprint 42", SprintId = 42,
            Labels = ["dependencies", "security"],
        },
        new()
        {
            Key = "DEMO-105", Summary = "Voting card animation causes layout shift on Safari",
            Status = "Estimation", IssueType = "Bug", Priority = "High",
            AssigneeName = "Dave Brown", ReporterName = "Bob Smith",
            SprintName = "Sprint 42", SprintId = 42,
            Labels = ["browser-compat"],
        },
        new()
        {
            Key = "DEMO-106", Summary = "Dark mode support for session view",
            Status = "Ready for Sprint", IssueType = "Story", Priority = "Medium",
            AssigneeName = "Carol Williams", ReporterName = "Carol Williams",
            SprintName = "Sprint 43", SprintId = 43,
        },
        new()
        {
            Key = "DEMO-107", Summary = "Integrate with Jira webhook for real-time issue sync",
            Status = "Approved", IssueType = "Story", Priority = "High",
            AssigneeName = "Bob Smith", ReporterName = "Alice Johnson",
            SprintName = "Sprint 43", SprintId = 43,
            Description = "<p>Currently the issue list is fetched once when a session is created. Changes made in Jira after that point (status updates, reassignments) are not reflected until the session is recreated.</p><p>Subscribe to Jira issue-updated webhooks so the session list stays in sync automatically.</p>",
        },
        new()
        {
            Key = "DEMO-108", Summary = "Refactor session storage to support Redis",
            Status = "Estimation", IssueType = "Task", Priority = "Medium",
            AssigneeName = "Dave Brown", ReporterName = "Dave Brown",
            SprintName = "Sprint 43", SprintId = 43,
            Labels = ["infrastructure"],
        },
        new()
        {
            Key = "DEMO-109", Summary = "Add onboarding tour for first-time users",
            Status = "Estimation", IssueType = "Story", Priority = "Low",
            AssigneeName = "Alice Johnson", ReporterName = "Carol Williams",
        },
        new()
        {
            Key = "DEMO-110", Summary = "Keyboard navigation for voting cards",
            Status = "Approved", IssueType = "Story", Priority = "Medium",
            AssigneeName = "Carol Williams", ReporterName = "Bob Smith",
            Labels = ["accessibility"],
        },
        new()
        {
            Key = "DEMO-111", Summary = "Performance audit for large backlogs (500+ items)",
            Status = "Estimation", IssueType = "Task", Priority = "Medium",
            AssigneeName = "Bob Smith", ReporterName = "Dave Brown",
        },
        new()
        {
            Key = "DEMO-112", Summary = "Fix race condition in concurrent vote submission",
            Status = "Ready for Sprint", IssueType = "Bug", Priority = "High",
            AssigneeName = "Dave Brown", ReporterName = "Bob Smith",
            Labels = ["reliability"],
            Description = "<p>Under load testing, two participants submitting votes within the same 50ms window occasionally results in one vote being silently dropped. The session shows N-1 votes with no indication a submission failed.</p>",
        },
    ];

    private static readonly List<string> Teams = ["Alpha", "Bravo"];

    public Task<List<JiraItem>> GetItemsAsync(string? team = null, string? jql = null) =>
        Task.FromResult(Items.ToList());

    public Task<JiraItem?> GetItemByKeyAsync(string key) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Key == key));

    public Task<List<string>> GetTeamsAsync() =>
        Task.FromResult(Teams.ToList());

    public string GetEffectiveJql(string? team = null) =>
        "project = DEMO AND \"Story Points\" IS EMPTY ORDER BY created DESC";

    public Task UpdateStoryPointsAsync(string issueKey, decimal points) =>
        Task.CompletedTask;
}
