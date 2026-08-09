using ClaudiumStudiosLauncher.Models;
using ClaudiumStudiosLauncher.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ClaudiumStudiosLauncher.Views;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer sliderTimer = new();
    private ShellViewModel? viewModel;
    private bool isPointerOverSlider;

    public HomePage()
    {
        InitializeComponent();
        var options = App.Host.Services.GetService<IOptions<SliderOptions>>()?.Value ?? new SliderOptions();
        sliderTimer.Interval = TimeSpan.FromSeconds(Math.Max(2, options.IntervalSeconds));
        sliderTimer.Tick += (_, _) => { if (options.AutoPlay && !isPointerOverSlider) ShowNextSlide(); };
        Loaded += OnLoaded;
        Unloaded += (_, _) => sliderTimer.Stop();
        KeyDown += OnKeyDown;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        viewModel = e.Parameter as ShellViewModel;
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        Focus(FocusState.Programmatic);
        if (viewModel is not null && viewModel.Games.Count == 0) await viewModel.InitializeAsync();
        EmptyState.Visibility = viewModel?.Games.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        if (viewModel?.Games.Count > 1) sliderTimer.Start();
        SlideIntroStoryboard.Begin();
    }

    private void OnSliderPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverSlider = true;
        sliderTimer.Stop();
        SliderControls.Opacity = 1;
    }

    private void OnSliderPointerExited(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverSlider = false;
        if (viewModel?.Games.Count > 1) sliderTimer.Start();
        SliderControls.Opacity = 0.86;
    }

    private void OnPreviousSlide(object sender, RoutedEventArgs args) => ShowPreviousSlide();
    private void OnNextSlide(object sender, RoutedEventArgs args) => ShowNextSlide();
    private void OnViewGame(object sender, RoutedEventArgs args) => Frame.Navigate(typeof(GamePage), viewModel);

    private void OnIndicatorClicked(object sender, RoutedEventArgs args)
    {
        if (viewModel is null || (sender as Button)?.Tag is not Game game) return;
        ChangeSlide(game);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Left) ShowPreviousSlide();
        if (args.Key == Windows.System.VirtualKey.Right) ShowNextSlide();
        if (args.Key == Windows.System.VirtualKey.Enter && viewModel?.SelectedGame is not null) Frame.Navigate(typeof(GamePage), viewModel);
    }

    private void ShowPreviousSlide()
    {
        if (viewModel is null || viewModel.Games.Count == 0) return;
        var index = Math.Max(0, viewModel.Games.IndexOf(viewModel.SelectedGame));
        ChangeSlide(viewModel.Games[(index - 1 + viewModel.Games.Count) % viewModel.Games.Count]);
    }

    private void ShowNextSlide()
    {
        if (viewModel is null || viewModel.Games.Count == 0) return;
        var index = Math.Max(0, viewModel.Games.IndexOf(viewModel.SelectedGame));
        ChangeSlide(viewModel.Games[(index + 1) % viewModel.Games.Count]);
    }

    private void ChangeSlide(Game game)
    {
        if (viewModel is null) return;
        SlideOutStoryboard.Begin();
        viewModel.SelectGame(game);
        SlideIntroStoryboard.Begin();
    }
}
