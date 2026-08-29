using Microsoft.UI.Xaml.Controls;
using PclModpackUpdater.ViewModels;

namespace PclModpackUpdater.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; } = new();

    public HomePage()
    {
        InitializeComponent();
        StatusInfoBar.CloseButtonClick += (_, _) => ViewModel.IsStatusOpen = false;
        _ = ViewModel.InitializeWithAutoCheckAsync();
    }
}
