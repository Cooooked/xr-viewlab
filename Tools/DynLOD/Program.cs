using System.IO;
using System.Windows;

namespace DynLOD;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var app = new Application();
            if (args.Contains("--self-test")) { SelfTest.Run(args); return 0; }
            return app.Run(new MainWindow(args.FirstOrDefault(a => a.EndsWith(".ini",StringComparison.OrdinalIgnoreCase))));
        }
        catch (Exception ex)
        {
            if (args.Contains("--self-test")) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test.txt"),ex.ToString()); return 1; }
            MessageBox.Show(ex.Message,"DynLOD",MessageBoxButton.OK,MessageBoxImage.Error); return 1;
        }
    }
}
