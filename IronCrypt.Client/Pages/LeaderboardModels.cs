namespace IronCrypt.Client.Pages;

public record LeaderboardEntry(string Username, int Value, int Kills, int Level, DateTime PlayedAt);
public record PersonalBestEntry(int Value, int Kills, int Level, DateTime PlayedAt);
