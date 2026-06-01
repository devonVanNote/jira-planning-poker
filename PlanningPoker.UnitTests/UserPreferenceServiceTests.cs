namespace PlanningPoker.UnitTests;

public class UserPreferenceServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IConfiguration _config;

    public UserPreferenceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PPPrefsTests_{Guid.NewGuid():N}");
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

    private UserPreferenceService CreateService() =>
        new(_config, NullLogger<UserPreferenceService>.Instance);

    // ── HasSeenParticipantGuide ──────────────────────────────────────────────

    [Fact]
    public void HasSeenParticipantGuide_NewUser_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenParticipantGuide("user1"));
    }

    [Fact]
    public void HasSeenParticipantGuide_EmptyUserId_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenParticipantGuide(""));
    }

    [Fact]
    public void HasSeenParticipantGuide_NullUserId_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenParticipantGuide(null!));
    }

    // ── HasSeenHostGuide ─────────────────────────────────────────────────────

    [Fact]
    public void HasSeenHostGuide_NewUser_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenHostGuide("user1"));
    }

    [Fact]
    public void HasSeenHostGuide_EmptyUserId_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenHostGuide(""));
    }

    [Fact]
    public void HasSeenHostGuide_NullUserId_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.HasSeenHostGuide(null!));
    }

    // ── MarkParticipantGuideSeen ─────────────────────────────────────────────

    [Fact]
    public void MarkParticipantGuideSeen_SetsFlag()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("user1");
        Assert.True(svc.HasSeenParticipantGuide("user1"));
    }

    [Fact]
    public void MarkParticipantGuideSeen_EmptyUserId_IsNoOp()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("");
        Assert.False(svc.HasSeenParticipantGuide(""));
    }

    [Fact]
    public void MarkParticipantGuideSeen_DoesNotAffectHostGuide()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("user1");
        Assert.False(svc.HasSeenHostGuide("user1"));
    }

    // ── MarkHostGuideSeen ────────────────────────────────────────────────────

    [Fact]
    public void MarkHostGuideSeen_SetsFlag()
    {
        var svc = CreateService();
        svc.MarkHostGuideSeen("user1");
        Assert.True(svc.HasSeenHostGuide("user1"));
    }

    [Fact]
    public void MarkHostGuideSeen_EmptyUserId_IsNoOp()
    {
        var svc = CreateService();
        svc.MarkHostGuideSeen("");
        Assert.False(svc.HasSeenHostGuide(""));
    }

    [Fact]
    public void MarkHostGuideSeen_DoesNotAffectParticipantGuide()
    {
        var svc = CreateService();
        svc.MarkHostGuideSeen("user1");
        Assert.False(svc.HasSeenParticipantGuide("user1"));
    }

    // ── Multi-user isolation ─────────────────────────────────────────────────

    [Fact]
    public void MultipleUsers_IndependentTracking()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("user1");
        Assert.True(svc.HasSeenParticipantGuide("user1"));
        Assert.False(svc.HasSeenParticipantGuide("user2"));
    }

    [Fact]
    public void BothGuides_CanBeMarkedForSameUser()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("user1");
        svc.MarkHostGuideSeen("user1");
        Assert.True(svc.HasSeenParticipantGuide("user1"));
        Assert.True(svc.HasSeenHostGuide("user1"));
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public void Persistence_PrefsReloadedOnNewInstance()
    {
        var svc1 = CreateService();
        svc1.MarkParticipantGuideSeen("user1");
        svc1.MarkHostGuideSeen("user2");

        var svc2 = CreateService();
        Assert.True(svc2.HasSeenParticipantGuide("user1"));
        Assert.True(svc2.HasSeenHostGuide("user2"));
    }

    [Fact]
    public void Persistence_PrefFileCreated()
    {
        var svc = CreateService();
        svc.MarkParticipantGuideSeen("user1");
        Assert.True(File.Exists(Path.Combine(_tempDir, "user-prefs.json")));
    }

    [Fact]
    public void Persistence_UnmarkedFlagsStillFalseAfterReload()
    {
        var svc1 = CreateService();
        svc1.MarkParticipantGuideSeen("user1");

        var svc2 = CreateService();
        Assert.False(svc2.HasSeenHostGuide("user1"));
        Assert.False(svc2.HasSeenParticipantGuide("user2"));
    }
}
