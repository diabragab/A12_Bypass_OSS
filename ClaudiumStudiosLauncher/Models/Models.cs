namespace ClaudiumStudiosLauncher.Models;

public enum GameInstallState { Offline, NotInstalled, Installing, Updating, Repairing, Ready, Running, UpdateRequired }
public enum ServerState { Unknown, Online, Offline, Maintenance }
public enum DownloadStatus { Queued, Downloading, Paused, Completed, Failed, Canceled, Verifying }

public sealed record Game(string Id, string Name, string Description, string Genre, string Version, string Banner, string Logo, string Cover, string Executable, string ManifestUrl, string ServerStatusUrl, string Developer = "Claudium Studios", string ReleaseDate = "TBA", string DownloadSize = "");
public sealed record GamesResponse(IReadOnlyList<Game> Games);
public sealed record InstalledGame(string Id, string Path, string Version);
public sealed record InstalledGamesStore(List<InstalledGame> Games);
public sealed record ManifestFile(string Path, long Size, string Sha256, string Url = "");
public sealed record GameManifest(string Game, string Version, IReadOnlyList<ManifestFile> Files);
public sealed record ServerStatus(string GameId, ServerState Status, int PlayersOnline, int Ping, string ServerVersion);
public sealed record NewsItem(string Id, string Title, string Description, string Image, DateTimeOffset Date, string GameId, string ReadMoreUrl);
public sealed record EventItem(string Id, string Title, string Description, string Banner, string GameId, DateTimeOffset StartDate, DateTimeOffset EndDate)
{
    public string Status => DateTimeOffset.UtcNow < StartDate ? "UPCOMING" : DateTimeOffset.UtcNow <= EndDate ? "LIVE" : "ENDED";
}
public sealed record UserAccount(string Username, string Email, string Avatar, IReadOnlyList<string> OwnedGames);
public sealed record AuthSession(string AccessToken, string RefreshToken, UserAccount Account);
public sealed class LauncherSettings
{
    public string DownloadLocation { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games", "Claudium Studios");
    public long MaximumDownloadBytesPerSecond { get; set; }
    public bool AutoUpdate { get; set; } = true;
    public bool LaunchOnWindowsStartup { get; set; }
    public bool MinimizeAfterGameStarts { get; set; } = true;
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Dark";
}
public sealed class DownloadJob
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string GameId { get; init; } = "";
    public string CurrentFile { get; set; } = "";
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double Progress => TotalBytes == 0 ? 0 : (double)DownloadedBytes / TotalBytes * 100;
    public long BytesPerSecond { get; set; }
    public TimeSpan Eta => BytesPerSecond <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((TotalBytes - DownloadedBytes) / Math.Max(1, BytesPerSecond));
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
}
