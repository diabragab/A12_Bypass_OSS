using System.Collections.ObjectModel;
using ClaudiumStudiosLauncher.Models;
using ClaudiumStudiosLauncher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudiumStudiosLauncher.ViewModels;

public partial class ShellViewModel(
    IApiService api,
    IGameService games,
    IGameInstallationService installer,
    IGameLaunchService launcher,
    INewsService news,
    IEventService events,
    IDownloadService downloads,
    ISettingsService settings,
    ILocalStore localStore,
    IAuthenticationService auth) : ObservableObject
{
    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<NewsItem> News { get; } = [];
    public ObservableCollection<EventItem> Events { get; } = [];
    public IReadOnlyList<DownloadJob> Downloads => downloads.Jobs;
    private InstalledGamesStore installedGames = new([]);

    [ObservableProperty] private Game? selectedGame;
    [ObservableProperty] private LauncherSettings launcherSettings = new();
    [ObservableProperty] private string offlineBanner = string.Empty;
    [ObservableProperty] private string selectedGameActionLabel = "INSTALL";
    [ObservableProperty] private string slideCounter = "00 / 00";

    public async Task InitializeAsync()
    {
        LauncherSettings = await settings.LoadAsync();
        installedGames = await localStore.LoadInstalledAsync();

        Games.Clear();
        foreach (var game in await games.GetGamesAsync()) Games.Add(game);
        SelectedGame = Games.FirstOrDefault();
        UpdateSlideCounter();
        OfflineBanner = api.IsOffline ? "OFFLINE MODE" : string.Empty;

        News.Clear();
        foreach (var item in await news.GetNewsAsync()) News.Add(item);

        Events.Clear();
        foreach (var item in await events.GetEventsAsync()) Events.Add(item);
    }

    [RelayCommand]
    private async Task PrimaryActionAsync(Game game)
    {
        if (SelectedGameActionLabel == "PLAY") await PlayAsync(game);
        else await InstallAsync(game);
    }

    [RelayCommand]
    private async Task PlayAsync(Game game)
    {
        SelectedGameActionLabel = "LAUNCHING...";
        await launcher.LaunchAsync(game);
        UpdateSelectedGameActionLabel();
    }

    [RelayCommand]
    private async Task InstallAsync(Game game)
    {
        SelectedGameActionLabel = "UPDATING...";
        await installer.InstallOrRepairAsync(game, Path.Combine(LauncherSettings.DownloadLocation, SafeName(game.Name)));
        installedGames = await localStore.LoadInstalledAsync();
        UpdateSelectedGameActionLabel();
    }

    [RelayCommand]
    private async Task RepairAsync(Game game) => await installer.InstallOrRepairAsync(game, Path.Combine(LauncherSettings.DownloadLocation, SafeName(game.Name)));

    [RelayCommand]
    private async Task UninstallAsync(Game game) => await installer.UninstallAsync(game);

    [RelayCommand]
    private async Task SaveSettingsAsync() => await settings.SaveAsync(LauncherSettings);

    [RelayCommand]
    private async Task LoginAsync((string email, string password, bool remember) input) => await auth.LoginAsync(input.email, input.password, input.remember);

    public void SelectGame(Game? game)
    {
        SelectedGame = game;
        UpdateSlideCounter();
    }

    partial void OnSelectedGameChanged(Game? value) => UpdateSelectedGameActionLabel();

    private void UpdateSelectedGameActionLabel()
    {
        if (SelectedGame is null)
        {
            SelectedGameActionLabel = "INSTALL";
            return;
        }

        var installed = installedGames.Games.FirstOrDefault(game => game.Id == SelectedGame.Id);
        SelectedGameActionLabel = installed is null ? "INSTALL" : string.Equals(installed.Version, SelectedGame.Version, StringComparison.OrdinalIgnoreCase) ? "PLAY" : "UPDATE";
    }

    private void UpdateSlideCounter()
    {
        var index = SelectedGame is null ? -1 : Games.IndexOf(SelectedGame);
        SlideCounter = Games.Count == 0 ? "00 / 00" : $"{index + 1:00} / {Games.Count:00}";
    }

    private static string SafeName(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
}
