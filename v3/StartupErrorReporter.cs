using System.IO;
using System.Windows;

namespace IT_viddil_monitoring;

internal static class StartupErrorReporter
{
    public static void Report(Exception error)
    {
        var log = Path.Combine(Path.GetTempPath(), "IT_viddil_monitoring_3.0.1-error.log");
        try { File.AppendAllText(log, $"{DateTimeOffset.Now:O}\n{error}\n\n"); }
        catch { log = "Не вдалося зберегти журнал помилки."; }
        MessageBox.Show("Програмі не вдалося продовжити роботу.\n\n" +
            error.Message + "\n\nЖурнал: " + log, "IT_viddil_monitoring 3.0.1",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
