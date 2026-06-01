using System.Collections.Concurrent;
using System.Text.Json;
using PlanningPoker.Models;

namespace PlanningPoker.Services;

public class UserPreferenceService : IUserPreferenceService
{
    private readonly ConcurrentDictionary<string, UserGuideState> _prefs = new();
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly ILogger<UserPreferenceService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public UserPreferenceService(IConfiguration config, ILogger<UserPreferenceService> logger)
    {
        _logger = logger;
        string dir = Path.GetFullPath(config["Sessions:StoragePath"] ?? "App_Data/sessions");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "user-prefs.json");
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }
        try
        {
            string json = File.ReadAllText(_filePath);
            Dictionary<string, UserGuideState>? saved =
                JsonSerializer.Deserialize<Dictionary<string, UserGuideState>>(json, JsonOptions);
            if (saved != null)
            {
                foreach (KeyValuePair<string, UserGuideState> kvp in saved)
                {
                    _prefs[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user prefs from {Path}", _filePath);
        }
    }

    private void SaveToDisk()
    {
        try
        {
            string json = JsonSerializer.Serialize(new Dictionary<string, UserGuideState>(_prefs), JsonOptions);
            string tmp = _filePath + ".tmp";
            lock (_writeLock)
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, _filePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user prefs to {Path}", _filePath);
        }
    }

    private UserGuideState GetOrCreate(string userId) =>
        _prefs.GetOrAdd(userId, _ => new UserGuideState());

    public bool HasSeenParticipantGuide(string userId) =>
        !string.IsNullOrEmpty(userId) && GetOrCreate(userId).SeenParticipantGuide;

    public bool HasSeenHostGuide(string userId) =>
        !string.IsNullOrEmpty(userId) && GetOrCreate(userId).SeenHostGuide;

    public void MarkParticipantGuideSeen(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }
        GetOrCreate(userId).SeenParticipantGuide = true;
        SaveToDisk();
    }

    public void MarkHostGuideSeen(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        GetOrCreate(userId).SeenHostGuide = true;
        SaveToDisk();
    }
}
