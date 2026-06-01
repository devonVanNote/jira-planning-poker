namespace PlanningPoker.Services;

public interface IUserPreferenceService
{
    bool HasSeenParticipantGuide(string userId);
    bool HasSeenHostGuide(string userId);
    void MarkParticipantGuideSeen(string userId);
    void MarkHostGuideSeen(string userId);
}
