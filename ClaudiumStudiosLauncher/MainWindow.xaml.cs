using ClaudiumStudiosLauncher.ViewModels;
using ClaudiumStudiosLauncher.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace ClaudiumStudiosLauncher;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        RootFrame.Loaded += (_, _) => AmbientStoryboard.Begin();
        RootFrame.Navigate(typeof(HomePage), viewModel);
    }

    private void OnSidebarNavigate(object sender, RoutedEventArgs args)
    {
        var tag = (sender as Button)?.Tag?.ToString();
        NavigateTo(tag);
    }

    private void NavigateTo(string? tag)
    {
        var page = tag switch
        {
            "Library" => typeof(LibraryPage),
            "Store" => typeof(StorePage),
            "News" => typeof(NewsPage),
            "Events" => typeof(EventsPage),
            "Downloads" => typeof(DownloadsPage),
            _ => typeof(HomePage)
        };

        var transition = new DrillInNavigationTransitionInfo();
        RootFrame.Navigate(page, ViewModel, transition);
    }

    private void OnMinimize(object sender, RoutedEventArgs args)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
    }

    private void OnMaximizeRestore(object sender, RoutedEventArgs args)
    {
        if (AppWindow.Presenter is not OverlappedPresenter presenter) return;
        if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore();
        else presenter.Maximize();
    }

    private void OnClose(object sender, RoutedEventArgs args) => Close();
}
