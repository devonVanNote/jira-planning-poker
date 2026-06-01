namespace PlanningPoker.UnitTests;

public class SessionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IConfiguration _config;

    public SessionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PPTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sessions:StoragePath"] = _tempDir })
            .Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SessionService CreateService() =>
        new(_config, NullLogger<SessionService>.Instance);

    private (SessionService Svc, PokerSession Session) Make(
        string hostId = "host1", string hostName = "Host",
        List<JiraItem>? items = null, string name = "Test Session")
    {
        var svc = CreateService();
        var session = svc.CreateSession(hostId, hostName, items ?? [], name);
        return (svc, session);
    }

    // ── CreateSession ────────────────────────────────────────────────────────

    [Fact]
    public void CreateSession_AssignsEightCharUpperId()
    {
        var (_, s) = Make();
        Assert.Equal(8, s.Id.Length);
        Assert.Equal(s.Id.ToUpper(), s.Id);
    }

    [Fact]
    public void CreateSession_SetsHostInfo()
    {
        var (_, s) = Make(hostId: "h1", hostName: "Alice");
        Assert.Equal("h1", s.HostId);
        Assert.Equal("Alice", s.HostName);
    }

    [Fact]
    public void CreateSession_SetsName()
    {
        var (_, s) = Make(name: "Sprint 5");
        Assert.Equal("Sprint 5", s.Name);
    }

    [Fact]
    public void CreateSession_SetsItems()
    {
        var items = new List<JiraItem> { new() { Key = "A-1" }, new() { Key = "A-2" } };
        var (_, s) = Make(items: items);
        Assert.Equal(2, s.Items.Count);
    }

    [Fact]
    public void CreateSession_PersistsToDisk()
    {
        Make();
        Assert.True(File.Exists(Path.Combine(_tempDir, "sessions.json")));
    }

    // ── GetSession ───────────────────────────────────────────────────────────

    [Fact]
    public void GetSession_ReturnsSession()
    {
        var (svc, s) = Make();
        Assert.NotNull(svc.GetSession(s.Id));
    }

    [Fact]
    public void GetSession_IsCaseInsensitive()
    {
        var (svc, s) = Make();
        Assert.NotNull(svc.GetSession(s.Id.ToLower()));
        Assert.NotNull(svc.GetSession(s.Id.ToUpper()));
    }

    [Fact]
    public void GetSession_UnknownId_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(svc.GetSession("ZZZZZZZZ"));
    }

    // ── JoinSession ──────────────────────────────────────────────────────────

    [Fact]
    public void JoinSession_AddsParticipant()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        var p = svc.GetSession(s.Id)!.Participants;
        Assert.Single(p);
        Assert.Equal("u1", p[0].Id);
        Assert.Equal("Alice", p[0].Name);
    }

    [Fact]
    public void JoinSession_HostIsNotAddedAsParticipant()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "h1", "Host");
        Assert.Empty(svc.GetSession(s.Id)!.Participants);
    }

    [Fact]
    public void JoinSession_DuplicateUser_OnlyOneEntry()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.JoinSession(s.Id, "u1", "Alice");
        Assert.Single(svc.GetSession(s.Id)!.Participants);
    }

    [Fact]
    public void JoinSession_AsObserver_SetsFlag()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Observer", isObserver: true);
        Assert.True(svc.GetSession(s.Id)!.Participants[0].IsObserver);
    }

    // ── LeaveSession ─────────────────────────────────────────────────────────

    [Fact]
    public void LeaveSession_RemovesParticipant()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.LeaveSession(s.Id, "u1");
        Assert.Empty(svc.GetSession(s.Id)!.Participants);
    }

    [Fact]
    public void LeaveSession_HostCannotLeave()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.LeaveSession(s.Id, "h1");
        Assert.NotNull(svc.GetSession(s.Id));
    }

    // ── SetObserverMode ──────────────────────────────────────────────────────

    [Fact]
    public void SetObserverMode_True_ClearsVote()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "u1", "5");
        svc.SetObserverMode(s.Id, "u1", true);
        var p = svc.GetSession(s.Id)!.Participants[0];
        Assert.True(p.IsObserver);
        Assert.False(p.HasVoted);
        Assert.Null(p.Vote);
    }

    [Fact]
    public void SetObserverMode_False_UpdatesFlag()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice", isObserver: true);
        svc.SetObserverMode(s.Id, "u1", false);
        Assert.False(svc.GetSession(s.Id)!.Participants[0].IsObserver);
    }

    // ── Vote ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Vote_Host_RecordsVote()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.Vote(s.Id, "h1", "8");
        var session = svc.GetSession(s.Id)!;
        Assert.True(session.HostHasVoted);
        Assert.Equal("8", session.HostVote);
    }

    [Fact]
    public void Vote_Participant_RecordsVote()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "u1", "3");
        var p = svc.GetSession(s.Id)!.Participants[0];
        Assert.True(p.HasVoted);
        Assert.Equal("3", p.Vote);
    }

    [Fact]
    public void Vote_Participant_ClearsNudgeFlag()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.NudgeParticipant(s.Id, "h1", "u1");
        svc.Vote(s.Id, "u1", "5");
        Assert.False(svc.GetSession(s.Id)!.Participants[0].IsNudged);
    }

    [Fact]
    public void Vote_Observer_IsIgnored()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice", isObserver: true);
        svc.Vote(s.Id, "u1", "5");
        var p = svc.GetSession(s.Id)!.Participants[0];
        Assert.False(p.HasVoted);
        Assert.Null(p.Vote);
    }

    // ── RevealVotes ──────────────────────────────────────────────────────────

    [Fact]
    public void RevealVotes_HostCanReveal()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.RevealVotes(s.Id, "h1");
        Assert.True(svc.GetSession(s.Id)!.VotesRevealed);
    }

    [Fact]
    public void RevealVotes_NonHostCannot()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.RevealVotes(s.Id, "u1");
        Assert.False(svc.GetSession(s.Id)!.VotesRevealed);
    }

    // ── ResetVoting ──────────────────────────────────────────────────────────

    [Fact]
    public void ResetVoting_ClearsAllVotingState()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "h1", "5");
        svc.Vote(s.Id, "u1", "3");
        svc.RevealVotes(s.Id, "h1");
        svc.ResetVoting(s.Id, "h1");
        var session = svc.GetSession(s.Id)!;
        Assert.False(session.VotesRevealed);
        Assert.False(session.HostHasVoted);
        Assert.Null(session.HostVote);
        Assert.Null(session.AgreedPoints);
        Assert.All(session.Participants, p => Assert.False(p.HasVoted));
        Assert.All(session.Participants, p => Assert.Null(p.Vote));
        Assert.All(session.Participants, p => Assert.False(p.IsNudged));
    }

    [Fact]
    public void ResetVoting_NonHostCannot()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.RevealVotes(s.Id, "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.ResetVoting(s.Id, "u1");
        Assert.True(svc.GetSession(s.Id)!.VotesRevealed);
    }

    // ── SetCurrentItem ───────────────────────────────────────────────────────

    [Fact]
    public void SetCurrentItem_HostCanSet()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.SetCurrentItem(s.Id, "h1", new JiraItem { Key = "A-1" });
        Assert.Equal("A-1", svc.GetSession(s.Id)!.CurrentItem?.Key);
    }

    [Fact]
    public void SetCurrentItem_ResetsVotingState()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "h1", "5");
        svc.Vote(s.Id, "u1", "3");
        svc.RevealVotes(s.Id, "h1");
        svc.SetCurrentItem(s.Id, "h1", new JiraItem { Key = "A-2" });
        var session = svc.GetSession(s.Id)!;
        Assert.False(session.VotesRevealed);
        Assert.False(session.HostHasVoted);
        Assert.Null(session.HostVote);
        Assert.Null(session.AgreedPoints);
        Assert.All(session.Participants, p => Assert.False(p.HasVoted));
        Assert.All(session.Participants, p => Assert.False(p.IsNudged));
    }

    [Fact]
    public void SetCurrentItem_NonHostCannot()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetCurrentItem(s.Id, "u1", new JiraItem { Key = "A-1" });
        Assert.Null(svc.GetSession(s.Id)!.CurrentItem);
    }

    // ── SetAgreedPoints ──────────────────────────────────────────────────────

    [Fact]
    public void SetAgreedPoints_HostCanSet()
    {
        var item = new JiraItem { Key = "A-1" };
        var (svc, s) = Make(hostId: "h1", items: [item]);
        svc.SetCurrentItem(s.Id, "h1", item);
        svc.SetAgreedPoints(s.Id, "h1", "8");
        Assert.Equal("8", svc.GetSession(s.Id)!.AgreedPoints);
    }

    [Fact]
    public void SetAgreedPoints_UpdatesCurrentItemAndListItem()
    {
        var item = new JiraItem { Key = "A-1" };
        var (svc, s) = Make(hostId: "h1", items: [item]);
        svc.SetCurrentItem(s.Id, "h1", item);
        svc.SetAgreedPoints(s.Id, "h1", "13");
        var session = svc.GetSession(s.Id)!;
        Assert.Equal("13", session.CurrentItem?.CurrentPoints);
        Assert.Equal("13", session.Items.First(i => i.Key == "A-1").CurrentPoints);
    }

    [Fact]
    public void SetAgreedPoints_NonHostCannot()
    {
        var item = new JiraItem { Key = "A-1" };
        var (svc, s) = Make(hostId: "h1", items: [item]);
        svc.SetCurrentItem(s.Id, "h1", item);
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetAgreedPoints(s.Id, "u1", "8");
        Assert.Null(svc.GetSession(s.Id)!.AgreedPoints);
    }

    // ── AddItem ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddItem_HostCanAdd()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.AddItem(s.Id, "h1", new JiraItem { Key = "NEW-1" });
        Assert.Single(svc.GetSession(s.Id)!.Items);
    }

    [Fact]
    public void AddItem_ParticipantCanAdd()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.AddItem(s.Id, "u1", new JiraItem { Key = "NEW-1" });
        Assert.Single(svc.GetSession(s.Id)!.Items);
    }

    [Fact]
    public void AddItem_NonMemberCannot()
    {
        var (svc, s) = Make();
        svc.AddItem(s.Id, "stranger", new JiraItem { Key = "NEW-1" });
        Assert.Empty(svc.GetSession(s.Id)!.Items);
    }

    [Fact]
    public void AddItem_DuplicateNotAdded()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.AddItem(s.Id, "h1", new JiraItem { Key = "NEW-1" });
        svc.AddItem(s.Id, "h1", new JiraItem { Key = "NEW-1" });
        Assert.Single(svc.GetSession(s.Id)!.Items);
    }

    // ── SetEmoji ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetEmoji_Host_SetsHostEmoji()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.SetEmoji(s.Id, "h1", "😀");
        Assert.Equal("😀", svc.GetSession(s.Id)!.HostEmoji);
    }

    [Fact]
    public void SetEmoji_Participant_SetsParticipantEmoji()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetEmoji(s.Id, "u1", "🎉");
        Assert.Equal("🎉", svc.GetSession(s.Id)!.Participants[0].Emoji);
    }

    [Fact]
    public void SetEmoji_Host_CanClear()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.SetEmoji(s.Id, "h1", "😀");
        svc.SetEmoji(s.Id, "h1", null);
        Assert.Null(svc.GetSession(s.Id)!.HostEmoji);
    }

    // ── ToggleItemPriority ───────────────────────────────────────────────────

    [Fact]
    public void ToggleItemPriority_AddsOnFirstCall()
    {
        var (svc, s) = Make();
        svc.ToggleItemPriority(s.Id, "h1", "A-1");
        Assert.Contains("A-1", svc.GetSession(s.Id)!.PriorityItems);
    }

    [Fact]
    public void ToggleItemPriority_RemovesOnSecondCall()
    {
        var (svc, s) = Make();
        svc.ToggleItemPriority(s.Id, "h1", "A-1");
        svc.ToggleItemPriority(s.Id, "h1", "A-1");
        Assert.DoesNotContain("A-1", svc.GetSession(s.Id)!.PriorityItems);
    }

    // ── SetHandRaised ────────────────────────────────────────────────────────

    [Fact]
    public void SetHandRaised_True_SetsFlag()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetHandRaised(s.Id, "u1", true);
        Assert.True(svc.GetSession(s.Id)!.Participants[0].HandRaised);
    }

    [Fact]
    public void SetHandRaised_False_ClearsFlag()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetHandRaised(s.Id, "u1", true);
        svc.SetHandRaised(s.Id, "u1", false);
        Assert.False(svc.GetSession(s.Id)!.Participants[0].HandRaised);
    }

    // ── NudgeParticipant ─────────────────────────────────────────────────────

    [Fact]
    public void NudgeParticipant_HostCanNudge()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.NudgeParticipant(s.Id, "h1", "u1");
        Assert.True(svc.GetSession(s.Id)!.Participants[0].IsNudged);
    }

    [Fact]
    public void NudgeParticipant_NonHostCannot()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.JoinSession(s.Id, "u2", "Bob");
        svc.NudgeParticipant(s.Id, "u2", "u1");
        Assert.False(svc.GetSession(s.Id)!.Participants[0].IsNudged);
    }

    // ── GetMaskedSession ─────────────────────────────────────────────────────

    [Fact]
    public void GetMaskedSession_UnknownId_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(svc.GetMaskedSession("ZZZZZZZZ"));
    }

    [Fact]
    public void GetMaskedSession_ParticipantVote_HiddenBeforeReveal()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "u1", "5");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Equal("voted", masked.Participants[0].Vote);
    }

    [Fact]
    public void GetMaskedSession_ParticipantVote_ShownAfterReveal()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.Vote(s.Id, "u1", "5");
        svc.RevealVotes(s.Id, "h1");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Equal("5", masked.Participants[0].Vote);
    }

    [Fact]
    public void GetMaskedSession_UnvotedParticipant_VoteIsNull()
    {
        var (svc, s) = Make();
        svc.JoinSession(s.Id, "u1", "Alice");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Null(masked.Participants[0].Vote);
    }

    [Fact]
    public void GetMaskedSession_HostVote_HiddenBeforeReveal()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.Vote(s.Id, "h1", "8");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Equal("voted", masked.HostVote);
    }

    [Fact]
    public void GetMaskedSession_HostVote_ShownAfterReveal()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.Vote(s.Id, "h1", "8");
        svc.RevealVotes(s.Id, "h1");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Equal("8", masked.HostVote);
    }

    [Fact]
    public void GetMaskedSession_UnvotedHost_VoteIsNull()
    {
        var (svc, s) = Make(hostId: "h1");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Null(masked.HostVote);
    }

    [Fact]
    public void GetMaskedSession_CopiesPriorityItems()
    {
        var (svc, s) = Make();
        svc.ToggleItemPriority(s.Id, "h1", "A-1");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.Contains("A-1", masked.PriorityItems);
    }

    [Fact]
    public void GetMaskedSession_CopiesParticipantFlags()
    {
        var (svc, s) = Make(hostId: "h1");
        svc.JoinSession(s.Id, "u1", "Alice");
        svc.SetHandRaised(s.Id, "u1", true);
        svc.NudgeParticipant(s.Id, "h1", "u1");
        var masked = svc.GetMaskedSession(s.Id)!;
        Assert.True(masked.Participants[0].HandRaised);
        Assert.True(masked.Participants[0].IsNudged);
    }

    // ── GetAll / Remove ──────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllSessions()
    {
        var svc = CreateService();
        svc.CreateSession("h1", "Host1", []);
        svc.CreateSession("h2", "Host2", []);
        Assert.Equal(2, svc.GetAll().Count());
    }

    [Fact]
    public void Remove_DeletesSession()
    {
        var (svc, s) = Make();
        svc.Remove(s.Id);
        Assert.Null(svc.GetSession(s.Id));
    }

    [Fact]
    public void Remove_FiresSessionChangedEvent()
    {
        var (svc, s) = Make();
        string? firedId = null;
        svc.SessionChanged += id => firedId = id;
        svc.Remove(s.Id);
        Assert.Equal(s.Id.ToUpper(), firedId);
    }

    // ── TryUpdate ────────────────────────────────────────────────────────────

    [Fact]
    public void TryUpdate_UnknownId_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.TryUpdate("ZZZZZZZZ", _ => { }));
    }

    [Fact]
    public void TryUpdate_KnownId_ReturnsTrue()
    {
        var (svc, s) = Make();
        Assert.True(svc.TryUpdate(s.Id, _ => { }));
    }

    [Fact]
    public void TryUpdate_FiresSessionChangedEvent()
    {
        var (svc, s) = Make();
        string? firedId = null;
        svc.SessionChanged += id => firedId = id;
        svc.TryUpdate(s.Id, _ => { });
        Assert.Equal(s.Id.ToUpper(), firedId);
    }

    // ── StoreSummary / ConsumeSummary ────────────────────────────────────────

    [Fact]
    public void StoreSummary_ConsumeSummary_ReturnsItems()
    {
        var svc = CreateService();
        var items = new List<JiraItem> { new() { Key = "X-1" } };
        svc.StoreSummary("sess1", items, "Sprint 1");
        var (name, result) = svc.ConsumeSummary("sess1");
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("X-1", result[0].Key);
        Assert.Equal("Sprint 1", name);
    }

    [Fact]
    public void ConsumeSummary_RemovesFromStore()
    {
        var svc = CreateService();
        svc.StoreSummary("sess1", [new JiraItem { Key = "X-1" }]);
        svc.ConsumeSummary("sess1");
        var (name, items) = svc.ConsumeSummary("sess1");
        Assert.Null(items);
        Assert.Null(name);
    }

    [Fact]
    public void ConsumeSummary_UnknownId_ReturnsNull()
    {
        var svc = CreateService();
        var (name, items) = svc.ConsumeSummary("nonexistent");
        Assert.Null(items);
        Assert.Null(name);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public void Persistence_SessionsReloadedOnNewInstance()
    {
        var (_, s) = Make(hostId: "h1", hostName: "Persisted Host");
        var svc2 = CreateService();
        var reloaded = svc2.GetSession(s.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("h1", reloaded.HostId);
        Assert.Equal("Persisted Host", reloaded.HostName);
    }

    [Fact]
    public void Persistence_MultipleSessionsReloaded()
    {
        var svc1 = CreateService();
        var s1 = svc1.CreateSession("h1", "Host1", []);
        var s2 = svc1.CreateSession("h2", "Host2", []);
        var svc2 = CreateService();
        Assert.NotNull(svc2.GetSession(s1.Id));
        Assert.NotNull(svc2.GetSession(s2.Id));
    }
}
