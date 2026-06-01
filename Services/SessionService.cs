using System.Collections.Concurrent;
using System.Text.Json;
using PlanningPoker.Models;

namespace PlanningPoker.Services;

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, PokerSession> _sessions = new();
    private readonly ConcurrentDictionary<string, List<JiraItem>> _summaries = new();
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly ILogger<SessionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public event Action<string>? SessionChanged;

    public SessionService(IConfiguration config, ILogger<SessionService> logger)
    {
        _logger = logger;
        string dir = Path.GetFullPath(config["Sessions:StoragePath"] ?? "App_Data/sessions");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "sessions.json");
        _logger.LogInformation("Session storage: {Path}", _filePath);
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("No session file found at {Path}, starting fresh", _filePath);
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            Dictionary<string, PokerSession>? saved = JsonSerializer.Deserialize<Dictionary<string, PokerSession>>(json, JsonOptions);
            if (saved != null)
            {
                foreach (KeyValuePair<string, PokerSession> kvp in saved)
                {
                    _sessions[kvp.Key] = kvp.Value;
                }

                _logger.LogInformation("Loaded {Count} session(s) from disk", saved.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sessions from {Path}", _filePath);
        }
    }

    private void SaveAllToDisk()
    {
        try
        {
            Dictionary<string, PokerSession> snapshot = new(_sessions);
            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            string tmp = _filePath + ".tmp";
            lock (_writeLock)
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, _filePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save sessions to {Path}", _filePath);
        }
    }

    public PokerSession? GetSession(string id) =>
        _sessions.TryGetValue(id.ToUpper(), out PokerSession? s) ? s : null;

    public PokerSession CreateSession(string hostId, string hostName, List<JiraItem> items, string sessionName = "")
    {
        PokerSession session = new PokerSession
        {
            Id = Guid.NewGuid().ToString("N")[..8].ToUpper(),
            Name = sessionName,
            HostId = hostId,
            HostName = hostName,
            Items = items
        };

        _sessions[session.Id] = session;
        SaveAllToDisk();
        return session;
    }

    public bool TryUpdate(string id, Action<PokerSession> update)
    {
        if (!_sessions.TryGetValue(id.ToUpper(), out PokerSession? session))
        {
            return false;
        }

        update(session);
        SaveAllToDisk();
        SessionChanged?.Invoke(id.ToUpper());
        return true;
    }

    public void JoinSession(string sessionId, string userId, string userName, bool isObserver = false)
    {
        TryUpdate(sessionId, s =>
        {
            if (s.HostId == userId)
            {
                return;
            }

            if (!s.Participants.Any(p => p.Id == userId))
            {
                s.Participants.Add(new Participant { Id = userId, Name = userName, IsObserver = isObserver });
            }
        });
    }

    public void SetObserverMode(string sessionId, string userId, bool isObserver)
    {
        TryUpdate(sessionId, s =>
        {
            Participant? p = s.Participants.FirstOrDefault(x => x.Id == userId);
            if (p == null)
            {
                return;
            }

            p.IsObserver = isObserver;
            if (isObserver)
            {
                p.HasVoted = false;
                p.Vote = null;
            }
        });
    }

    public void LeaveSession(string sessionId, string userId)
    {
        TryUpdate(sessionId, s =>
        {
            if (s.HostId == userId)
            {
                return;
            }

            s.Participants.RemoveAll(p => p.Id == userId);
        });
    }

    public void Vote(string sessionId, string userId, string cardValue)
    {
        TryUpdate(sessionId, s =>
        {
            if (s.HostId == userId)
            {
                s.HostHasVoted = true;
                s.HostVote = cardValue;
            }
            else
            {
                Participant? p = s.Participants.FirstOrDefault(x => x.Id == userId);
                if (p != null && !p.IsObserver)
                {
                    p.HasVoted = true;
                    p.Vote = cardValue;
                    p.IsNudged = false;
                }
            }
        });
    }

    public void RevealVotes(string sessionId, string hostId)
    {
        PokerSession? session = GetSession(sessionId);
        if (session?.HostId != hostId)
        {
            return;
        }

        TryUpdate(sessionId, s => s.VotesRevealed = true);
    }

    public void ResetVoting(string sessionId, string hostId)
    {
        PokerSession? session = GetSession(sessionId);
        if (session?.HostId != hostId)
        {
            return;
        }

        TryUpdate(sessionId, s =>
        {
            s.VotesRevealed = false;
            s.HostHasVoted = false;
            s.HostVote = null;
            s.AgreedPoints = null;
            foreach (Participant p in s.Participants)
            {
                p.HasVoted = false;
                p.Vote = null;
                p.IsNudged = false;
            }
        });
    }

    public void SetCurrentItem(string sessionId, string hostId, JiraItem item)
    {
        PokerSession? session = GetSession(sessionId);
        if (session?.HostId != hostId)
        {
            return;
        }

        TryUpdate(sessionId, s =>
        {
            s.CurrentItem = item;
            s.VotesRevealed = false;
            s.HostHasVoted = false;
            s.HostVote = null;
            s.AgreedPoints = null;

            foreach (Participant p in s.Participants)
            {
                p.HasVoted = false;
                p.Vote = null;
                p.IsNudged = false;
            }
        });
    }

    public void SetAgreedPoints(string sessionId, string hostId, string points)
    {
        PokerSession? session = GetSession(sessionId);
        if (session?.HostId != hostId)
        {
            return;
        }

        TryUpdate(sessionId, s =>
        {
            s.AgreedPoints = points;
            if (s.CurrentItem != null)
            {
                JiraItem? item = s.Items.FirstOrDefault(i => i.Key == s.CurrentItem.Key);
                if (item != null)
                {
                    item.CurrentPoints = points;
                }
                s.CurrentItem.CurrentPoints = points;
            }
        });
    }

    public void AddItem(string sessionId, string userId, JiraItem item)
    {
        PokerSession? session = GetSession(sessionId);
        if (session == null)
        {
            return;
        }
        bool isMember = session.HostId == userId || session.Participants.Any(p => p.Id == userId);
        if (!isMember)
        {
            return;
        }

        TryUpdate(sessionId, s =>
        {
            if (!s.Items.Any(i => i.Key == item.Key))
            {
                s.Items.Add(item);
            }
        });
    }

    public void SetEmoji(string sessionId, string userId, string? emoji)
    {
        PokerSession? session = GetSession(sessionId);
        if (session == null)
        {
            return;
        }

        if (session.HostId == userId)
        {
            TryUpdate(sessionId, s => s.HostEmoji = emoji);
        }
        else
        {
            TryUpdate(sessionId, s =>
            {
                Participant? p = s.Participants.FirstOrDefault(x => x.Id == userId);
                p?.Emoji = emoji;
            });
        }
    }

    public void ToggleItemPriority(string sessionId, string userId, string itemKey)
    {
        TryUpdate(sessionId, s =>
        {
            if (!s.PriorityItems.Add(itemKey))
            {
                s.PriorityItems.Remove(itemKey);
            }
        });
    }

    public void SetHandRaised(string sessionId, string participantId, bool raised)
    {
        TryUpdate(sessionId, s =>
        {
            Participant? p = s.Participants.FirstOrDefault(x => x.Id == participantId);
            if (p != null)
            {
                p.HandRaised = raised;
            }
        });
    }

    public void NudgeParticipant(string sessionId, string hostId, string participantId)
    {
        PokerSession? session = GetSession(sessionId);
        if (session?.HostId != hostId)
        {
            return;
        }

        TryUpdate(sessionId, s =>
        {
            Participant? p = s.Participants.FirstOrDefault(x => x.Id == participantId);

            p?.IsNudged = true;
        });
    }

    public PokerSession? GetMaskedSession(string id)
    {
        PokerSession? s = GetSession(id);
        return s == null
            ? null
            : new PokerSession
            {
                Id = s.Id,
                Name = s.Name,
                HostId = s.HostId,
                HostName = s.HostName,
                HostHasVoted = s.HostHasVoted,
                HostVote = s.VotesRevealed ? s.HostVote : (s.HostHasVoted ? "voted" : null),
                HostEmoji = s.HostEmoji,
                Items = s.Items,
                CurrentItem = s.CurrentItem,
                VotesRevealed = s.VotesRevealed,
                AgreedPoints = s.AgreedPoints,
                PriorityItems = [.. s.PriorityItems],
                Participants = [.. s.Participants.Select(p => new Participant
                {
                    Id = p.Id,
                    Name = p.Name,
                    HasVoted = p.HasVoted,
                    Vote = s.VotesRevealed ? p.Vote : (p.HasVoted ? "voted" : null),
                    Emoji = p.Emoji,
                    HandRaised = p.HandRaised,
                    IsObserver = p.IsObserver,
                    IsNudged = p.IsNudged
                })]
            };
    }

    public IEnumerable<PokerSession> GetAll() => _sessions.Values;

    public void Remove(string id)
    {
        _sessions.TryRemove(id.ToUpper(), out _);
        SaveAllToDisk();
        SessionChanged?.Invoke(id.ToUpper());
    }

    public void StoreSummary(string sessionId, List<JiraItem> pointedItems) =>
        _summaries[sessionId.ToUpper()] = pointedItems;

    public List<JiraItem>? ConsumeSummary(string sessionId)
    {
        _summaries.TryRemove(sessionId.ToUpper(), out List<JiraItem>? items);
        return items;
    }
}
