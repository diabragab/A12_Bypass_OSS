using ClaudiumStudiosLauncher.Models;
using ClaudiumStudiosLauncher.Services;
using ClaudiumStudiosLauncher.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;

namespace ClaudiumStudiosLauncher;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddJsonFile("appsettings.json", false, true))
            .ConfigureServices((context, services) =>
            {
                var sliderOptions = new SliderOptions
                {
                    AutoPlay = bool.TryParse(context.Configuration["Slider:AutoPlay"], out var autoPlay) ? autoPlay : true,
                    IntervalSeconds = int.TryParse(context.Configuration["Slider:IntervalSeconds"], out var intervalSeconds) ? intervalSeconds : 6
                };

                services.AddSingleton<IOptions<SliderOptions>>(Options.Create(sliderOptions));
                services.AddHttpClient<IApiService, ApiService>(client => client.BaseAddress = new Uri(context.Configuration["Api:BaseUrl"]!));
                services.AddHttpClient<IDownloadService, DownloadService>();
                services.AddHttpClient<IUpdateService, UpdateService>();
                services.AddHttpClient<IServerStatusService, ServerStatusService>();
                services.AddSingleton<ILocalStore, LocalStore>();
                services.AddSingleton<IAuthenticationService, AuthenticationService>();
                services.AddSingleton<IGameService, GameService>();
                services.AddSingleton<IGameInstallationService, GameInstallationService>();
                services.AddSingleton<IGameLaunchService, GameLaunchService>();
                services.AddSingleton<INewsService, NewsService>();
                services.AddSingleton<IEventService, EventService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await Host.StartAsync();
        Host.Services.GetRequiredService<MainWindow>().Activate();
    }
}
