using System.Windows.Controls;
using DSX.Core.Models;
using DSX.Core.ViewModels;
using DSX.WPF.Controls;

namespace DSX.WPF.Views.Pages;

public partial class ControllerMappingPage : UserControl
{
    public ControllerMappingPage()
    {
        InitializeComponent();
        ControllerView.ButtonClicked += OnControllerButtonClicked;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        WireViewModel();
        DataContextChanged += (s, args) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (DataContext is ControllerMappingViewModel vm)
        {
            vm.ControllerButtonClicked -= OnVmButtonClicked;
            vm.ControllerButtonClicked += OnVmButtonClicked;
            ControllerView.SelectedButton = vm.SelectedControllerButton;
        }
    }

    private void OnControllerButtonClicked(object? sender, ControllerButtonEventArgs e)
    {
        if (DataContext is ControllerMappingViewModel vm)
            vm.HandleControllerButtonClicked(e.Button);
    }

    private void OnVmButtonClicked(object? sender, ControllerButton button)
    {
        ControllerView.SelectedButton = button;
    }
}
