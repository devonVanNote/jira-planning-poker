namespace PlanningPoker.UnitTests;

public class ModelTests
{
    [Fact]
    public void StoryPoints_ContainsExpectedValues()
    {
        string[] expected = ["1", "2", "3", "5", "8", "13", "☕"];
        Assert.Equal(expected, StoryPoints.Values);
    }

    [Fact]
    public void PokerSession_DefaultProperties()
    {
        var session = new PokerSession();
        Assert.Equal("", session.Id);
        Assert.Equal("", session.Name);
        Assert.Equal("", session.HostId);
        Assert.Equal("", session.HostName);
        Assert.False(session.HostHasVoted);
        Assert.Null(session.HostVote);
        Assert.Null(session.HostEmoji);
        Assert.Empty(session.Participants);
        Assert.Empty(session.Items);
        Assert.Null(session.CurrentItem);
        Assert.False(session.VotesRevealed);
        Assert.Null(session.AgreedPoints);
        Assert.Empty(session.PriorityItems);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
        Assert.True(session.CreatedAt > DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void PokerSession_PriorityItems_IsHashSet_NoDuplicates()
    {
        var session = new PokerSession();
        session.PriorityItems.Add("ITEM-1");
        session.PriorityItems.Add("ITEM-1");
        Assert.Single(session.PriorityItems);
    }

    [Fact]
    public void Participant_DefaultProperties()
    {
        var p = new Participant();
        Assert.Equal("", p.Id);
        Assert.Equal("", p.Name);
        Assert.False(p.HasVoted);
        Assert.Null(p.Vote);
        Assert.Null(p.Emoji);
        Assert.False(p.HandRaised);
        Assert.False(p.IsObserver);
        Assert.False(p.IsNudged);
    }

    [Fact]
    public void JiraItem_DefaultProperties()
    {
        var item = new JiraItem();
        Assert.Equal("", item.Key);
        Assert.Equal("", item.Summary);
        Assert.Null(item.Description);
        Assert.Null(item.CurrentPoints);
        Assert.Equal("", item.Status);
        Assert.Null(item.IssueType);
        Assert.Null(item.Priority);
        Assert.Null(item.AssigneeName);
        Assert.Null(item.ReporterName);
        Assert.Empty(item.Labels);
        Assert.Null(item.TeamName);
        Assert.Null(item.SprintName);
        Assert.Null(item.SprintId);
    }

    [Fact]
    public void UserGuideState_DefaultProperties()
    {
        var state = new UserGuideState();
        Assert.False(state.SeenParticipantGuide);
        Assert.False(state.SeenHostGuide);
    }

    [Fact]
    public void UserGuideState_PropertiesAreSettable()
    {
        var state = new UserGuideState { SeenParticipantGuide = true, SeenHostGuide = true };
        Assert.True(state.SeenParticipantGuide);
        Assert.True(state.SeenHostGuide);
    }
}
