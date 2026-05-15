namespace Ollamock.App;

public partial class MainPage : ContentPage
{
    public MainPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
