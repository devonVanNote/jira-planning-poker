using PlanningPoker.Models;

namespace PlanningPoker.Services;

public interface IJiraService
{
    Task<List<JiraItem>> GetItemsAsync(string? team = null, string? jql = null);
    Task<JiraItem?> GetItemByKeyAsync(string key);
    Task<List<string>> GetTeamsAsync();
    string GetEffectiveJql(string? team = null);
    Task UpdateStoryPointsAsync(string issueKey, decimal points);
}
