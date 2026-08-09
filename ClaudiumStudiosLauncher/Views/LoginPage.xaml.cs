using ClaudiumStudiosLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;
namespace ClaudiumStudiosLauncher.Views;
public sealed partial class LoginPage : Page { public ShellViewModel? ViewModel { get; private set; } public LoginPage(){ InitializeComponent(); Loaded += async (_,__)=>{ if(ViewModel is not null && ViewModel.Games.Count==0) await ViewModel.InitializeAsync(); }; } protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e){ ViewModel=e.Parameter as ShellViewModel; DataContext=ViewModel; } }
