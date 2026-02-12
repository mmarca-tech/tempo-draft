using System.Runtime.Versioning;

namespace TempoDraft.Desktop;

internal static class Program
{
    [STAThread]
    [SupportedOSPlatform("windows")]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            MessageBox.Show(e.Exception.ToString(), "TempoDraft Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TempoDraft Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
