namespace PlanningPoker.Models;

public static class StoryPoints
{
    public static string[] Values { get; private set; } = ["1", "2", "3", "5", "8", "13", "☕"];

    internal static void Initialize(string[] values) => Values = values;
}
