namespace PlanningPoker.Models;

public class Participant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool HasVoted { get; set; }
    public string? Vote { get; set; }
    public string? Emoji { get; set; }
    public bool HandRaised { get; set; }
    public bool IsObserver { get; set; }
    public bool IsNudged { get; set; }
    public bool IsConnected { get; set; } = true;
}
