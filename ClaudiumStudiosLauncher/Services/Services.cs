using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClaudiumStudiosLauncher.Models;

namespace ClaudiumStudiosLauncher.Services;

public interface IApiService
{
    bool IsOffline { get; }
    Task<T?> GetAsync<T>(string path, CancellationToken ct = default);
    Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default);
}

public sealed class ApiService(HttpClient http) : IApiService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public bool IsOffline { get; private set; }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            IsOffline = false;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (HttpRequestException)
        {
            IsOffline = true;
            return default;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            IsOffline = true;
            return default;
        }
    }

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(path, body, Json, ct);
        response.EnsureSuccessStatusCode();
        IsOffline = false;
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }
}

public interface IAuthenticationService
{
    AuthSession? Session { get; }
    Task<AuthSession?> LoginAsync(string email, string password, bool remember, CancellationToken ct = default);
    Task RegisterAsync(string username, string email, string password, CancellationToken ct = default);
    void Logout();
}

public sealed class AuthenticationService(IApiService api) : IAuthenticationService
{
    public AuthSession? Session { get; private set; }

    public async Task<AuthSession?> LoginAsync(string email, string password, bool remember, CancellationToken ct = default)
    {
        Session = await api.PostAsync<AuthSession>("auth/login", new { email, password, remember }, ct);
        return Session;
    }

    public async Task RegisterAsync(string username, string email, string password, CancellationToken ct = default)
    {
        _ = await api.PostAsync<object>("auth/register", new { username, email, password }, ct);
    }

    public void Logout() => Session = null;
}

public interface IGameService
{
    Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken ct = default);
}

public sealed class GameService(IApiService api, ILocalStore local) : IGameService
{
    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken ct = default)
    {
        var response = await api.GetAsync<GamesResponse>("games", ct);
        if (response?.Games is { Count: > 0 } games)
        {
            await local.CacheGamesAsync(games, ct);
            return games;
        }

        return await local.GetCachedGamesAsync(ct);
    }
}

public interface ILocalStore
{
    Task<IReadOnlyList<Game>> GetCachedGamesAsync(CancellationToken ct = default);
    Task CacheGamesAsync(IEnumerable<Game> games, CancellationToken ct = default);
    Task<InstalledGamesStore> LoadInstalledAsync(CancellationToken ct = default);
    Task SaveInstalledAsync(InstalledGamesStore store, CancellationToken ct = default);
}

public sealed class LocalStore : ILocalStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudium Studios", "Launcher");
    private string Installed => Path.Combine(root, "installed_games.json");
    private string Cache => Path.Combine(root, "games_cache.json");

    public async Task<IReadOnlyList<Game>> GetCachedGamesAsync(CancellationToken ct = default)
    {
        if (!File.Exists(Cache)) return [];
        await using var stream = File.OpenRead(Cache);
        return (await JsonSerializer.DeserializeAsync<GamesResponse>(stream, Json, ct))?.Games ?? [];
    }

    public async Task CacheGamesAsync(IEnumerable<Game> games, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root);
        await using var stream = File.Create(Cache);
        await JsonSerializer.SerializeAsync(stream, new GamesResponse(games.ToList()), Json, ct);
    }

    public async Task<InstalledGamesStore> LoadInstalledAsync(CancellationToken ct = default)
    {
        if (!File.Exists(Installed)) return new InstalledGamesStore([]);
        await using var stream = File.OpenRead(Installed);
        return await JsonSerializer.DeserializeAsync<InstalledGamesStore>(stream, Json, ct) ?? new InstalledGamesStore([]);
    }

    public async Task SaveInstalledAsync(InstalledGamesStore store, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root);
        await using var stream = File.Create(Installed);
        await JsonSerializer.SerializeAsync(stream, store, Json, ct);
    }
}

public interface IDownloadService
{
    IReadOnlyList<DownloadJob> Jobs { get; }
    Task DownloadFileAsync(Uri uri, string destination, string sha256, DownloadJob job, CancellationToken ct = default);
}

public sealed class DownloadService(HttpClient http) : IDownloadService
{
    private readonly List<DownloadJob> jobs = [];
    public IReadOnlyList<DownloadJob> Jobs => jobs;

    public async Task DownloadFileAsync(Uri uri, string destination, string sha256, DownloadJob job, CancellationToken ct = default)
    {
        if (uri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Game downloads must use HTTPS.");
        if (!jobs.Contains(job)) jobs.Add(job);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var partialPath = destination + ".part";
        var existingBytes = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existingBytes > 0) request.Headers.Range = new RangeHeaderValue(existingBytes, null);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        job.DownloadedBytes = existingBytes;
        job.TotalBytes = (response.Content.Headers.ContentLength ?? 0) + existingBytes;
        job.Status = DownloadStatus.Downloading;

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(partialPath, FileMode.Append, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        var stopwatch = Stopwatch.StartNew();
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            job.DownloadedBytes += read;
            job.BytesPerSecond = (long)(job.DownloadedBytes / Math.Max(1, stopwatch.Elapsed.TotalSeconds));
        }

        await output.FlushAsync(ct);
        output.Close();
        job.Status = DownloadStatus.Verifying;

        if (!string.IsNullOrWhiteSpace(sha256) && !await Sha256MatchesAsync(partialPath, sha256, ct))
        {
            job.Status = DownloadStatus.Failed;
            throw new InvalidDataException("SHA-256 verification failed.");
        }

        File.Move(partialPath, destination, true);
        job.Status = DownloadStatus.Completed;
    }

    public static async Task<bool> Sha256MatchesAsync(string path, string expected, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}

public interface IUpdateService
{
    Task<GameManifest?> GetManifestAsync(Game game, CancellationToken ct = default);
    Task<IReadOnlyList<ManifestFile>> GetFilesNeedingDownloadAsync(GameManifest manifest, string installPath, CancellationToken ct = default);
}

public sealed class UpdateService(HttpClient http) : IUpdateService
{
    public async Task<GameManifest?> GetManifestAsync(Game game, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(game.ManifestUrl)) return null;
        var manifestUri = new Uri(game.ManifestUrl, UriKind.Absolute);
        if (manifestUri.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Remote manifests must use HTTPS.");
        return await http.GetFromJsonAsync<GameManifest>(manifestUri, ct);
    }

    public async Task<IReadOnlyList<ManifestFile>> GetFilesNeedingDownloadAsync(GameManifest manifest, string installPath, CancellationToken ct = default)
    {
        var required = new List<ManifestFile>();
        var installRoot = Path.GetFullPath(installPath);
        foreach (var file in manifest.Files)
        {
            var destination = Path.GetFullPath(Path.Combine(installRoot, file.Path));
            if (!destination.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Manifest path traversal blocked.");
            }

            if (!File.Exists(destination) || new FileInfo(destination).Length != file.Size || !await DownloadService.Sha256MatchesAsync(destination, file.Sha256, ct))
            {
                required.Add(file);
            }
        }

        return required;
    }
}

public interface IGameInstallationService
{
    Task InstallOrRepairAsync(Game game, string installPath, CancellationToken ct = default);
    Task UninstallAsync(Game game, CancellationToken ct = default);
}

public sealed class GameInstallationService(IUpdateService updates, IDownloadService downloads, ILocalStore store) : IGameInstallationService
{
    public async Task InstallOrRepairAsync(Game game, string installPath, CancellationToken ct = default)
    {
        var manifest = await updates.GetManifestAsync(game, ct) ?? throw new InvalidOperationException("Missing game manifest.");
        var installRoot = Path.GetFullPath(installPath);
        Directory.CreateDirectory(installRoot);

        foreach (var file in await updates.GetFilesNeedingDownloadAsync(manifest, installRoot, ct))
        {
            var destination = Path.GetFullPath(Path.Combine(installRoot, file.Path));
            if (!destination.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Manifest path traversal blocked.");
            var fileUri = string.IsNullOrWhiteSpace(file.Url) ? new Uri(new Uri(game.ManifestUrl), file.Path) : new Uri(file.Url, UriKind.Absolute);
            await downloads.DownloadFileAsync(fileUri, destination, file.Sha256, new DownloadJob { GameId = game.Id, CurrentFile = file.Path }, ct);
        }

        var installed = await store.LoadInstalledAsync(ct);
        installed.Games.RemoveAll(existing => existing.Id == game.Id);
        installed.Games.Add(new InstalledGame(game.Id, installRoot, manifest.Version));
        await store.SaveInstalledAsync(installed, ct);
    }

    public async Task UninstallAsync(Game game, CancellationToken ct = default)
    {
        var installed = await store.LoadInstalledAsync(ct);
        var item = installed.Games.FirstOrDefault(existing => existing.Id == game.Id);
        if (item is not null && Directory.Exists(item.Path)) Directory.Delete(item.Path, true);
        installed.Games.RemoveAll(existing => existing.Id == game.Id);
        await store.SaveInstalledAsync(installed, ct);
    }
}

public interface IGameLaunchService
{
    Task LaunchAsync(Game game, CancellationToken ct = default);
}

public sealed class GameLaunchService(ILocalStore store) : IGameLaunchService
{
    public async Task LaunchAsync(Game game, CancellationToken ct = default)
    {
        var installed = await store.LoadInstalledAsync(ct);
        var item = installed.Games.FirstOrDefault(existing => existing.Id == game.Id) ?? throw new InvalidOperationException("Game is not installed.");
        var installRoot = Path.GetFullPath(item.Path);
        var executable = Path.GetFullPath(Path.Combine(installRoot, game.Executable));
        if (!executable.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
        {
            throw new InvalidOperationException("Executable blocked or missing.");
        }

        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)).Any()) return;
        Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false });
    }
}

public interface IServerStatusService
{
    Task<ServerStatus> GetStatusAsync(Game game, CancellationToken ct = default);
}

public sealed class ServerStatusService(HttpClient http) : IServerStatusService
{
    public async Task<ServerStatus> GetStatusAsync(Game game, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(game.ServerStatusUrl)) return new ServerStatus(game.Id, ServerState.Unknown, 0, 0, game.Version);
            return await http.GetFromJsonAsync<ServerStatus>(game.ServerStatusUrl, ct) ?? new ServerStatus(game.Id, ServerState.Unknown, 0, 0, game.Version);
        }
        catch (HttpRequestException)
        {
            return new ServerStatus(game.Id, ServerState.Offline, 0, 0, game.Version);
        }
    }
}

public interface INewsService { Task<IReadOnlyList<NewsItem>> GetNewsAsync(CancellationToken ct = default); }
public sealed class NewsService(IApiService api) : INewsService { public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(CancellationToken ct = default) => await api.GetAsync<IReadOnlyList<NewsItem>>("news", ct) ?? []; }

public interface IEventService { Task<IReadOnlyList<EventItem>> GetEventsAsync(CancellationToken ct = default); }
public sealed class EventService(IApiService api) : IEventService { public async Task<IReadOnlyList<EventItem>> GetEventsAsync(CancellationToken ct = default) => await api.GetAsync<IReadOnlyList<EventItem>>("events", ct) ?? []; }

public interface ISettingsService { Task<LauncherSettings> LoadAsync(CancellationToken ct = default); Task SaveAsync(LauncherSettings settings, CancellationToken ct = default); }
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudium Studios", "Launcher", "settings.json");

    public async Task<LauncherSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(file)) return new LauncherSettings();
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<LauncherSettings>(stream, Json, ct) ?? new LauncherSettings();
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await using var stream = File.Create(file);
        await JsonSerializer.SerializeAsync(stream, settings, Json, ct);
    }
}
