using System.Windows.Controls;
using OnAirCut.RenderServer.ViewModels;

namespace OnAirCut.RenderServer.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel vm)
        {
            vm.ScrollRequested += (_, _) =>
            {
                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
            };
        }
    }
}
