namespace PlanningPoker.Models;

public class JiraItem
{
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public string? CurrentPoints { get; set; }
    public string Status { get; set; } = "";
    public string? IssueType { get; set; }
    public string? Priority { get; set; }
    public string? AssigneeName { get; set; }
    public string? ReporterName { get; set; }
    public List<string> Labels { get; set; } = [];
    public string? TeamName { get; set; }
    public string? SprintName { get; set; }
    public int? SprintId { get; set; }
}
