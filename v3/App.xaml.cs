using System.Globalization;
using System.Windows;

namespace IT_viddil_monitoring;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            StartupErrorReporter.Report(args.Exception);
            Shutdown(1);
        };
        base.OnStartup(e);
    }
}
