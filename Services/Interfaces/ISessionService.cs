using PlanningPoker.Models;

namespace PlanningPoker.Services;

public interface ISessionService
{
    event Action<string>? SessionChanged;

    PokerSession CreateSession(string hostId, string hostName, List<JiraItem> items, string sessionName = "");
    PokerSession? GetSession(string id);
    PokerSession? GetMaskedSession(string id);
    IEnumerable<PokerSession> GetAll();

    bool TryUpdate(string id, Action<PokerSession> update);
    void JoinSession(string sessionId, string userId, string userName, bool isObserver = false);
    void LeaveSession(string sessionId, string userId);
    void SetObserverMode(string sessionId, string userId, bool isObserver);
    void Vote(string sessionId, string userId, string cardValue);
    void RevealVotes(string sessionId, string hostId);
    void ResetVoting(string sessionId, string hostId);
    void SetCurrentItem(string sessionId, string hostId, JiraItem item);
    void SetAgreedPoints(string sessionId, string hostId, string points);
    void AddItem(string sessionId, string hostId, JiraItem item);
    void SetEmoji(string sessionId, string userId, string? emoji);
    void ToggleItemPriority(string sessionId, string userId, string itemKey);
    void SetHandRaised(string sessionId, string participantId, bool raised);
    void NudgeParticipant(string sessionId, string hostId, string participantId);
    void Remove(string id);
    void StoreSummary(string sessionId, List<JiraItem> pointedItems);
    List<JiraItem>? ConsumeSummary(string sessionId);
}
