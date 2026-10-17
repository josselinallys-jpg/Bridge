using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Bridge.Core.Logging;

namespace Bridge.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LoggerProvider.GetLogger().Error($"Erreur non gérée : {args.Exception}");
            MessageBox.Show($"Une erreur est survenue : {args.Exception.Message}", "Bridge",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

