using System.Windows;
using MagicPanel.Core;

namespace MagicPanel;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            AppLog.Write("UNHANDLED: " + e.Exception);
            MessageBox.Show(e.Exception.Message, "Magic Panel", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
