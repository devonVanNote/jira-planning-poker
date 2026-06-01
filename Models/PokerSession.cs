namespace PlanningPoker.Models;

public class PokerSession
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string HostId { get; set; } = "";
    public string HostName { get; set; } = "";
    public bool HostHasVoted { get; set; }
    public string? HostVote { get; set; }
    public string? HostEmoji { get; set; }
    public List<Participant> Participants { get; set; } = [];
    public List<JiraItem> Items { get; set; } = [];
    public JiraItem? CurrentItem { get; set; }
    public bool VotesRevealed { get; set; }
    public string? AgreedPoints { get; set; }
    public HashSet<string> PriorityItems { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
