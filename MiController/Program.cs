using System;
using System.Windows.Forms;

namespace MiController
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string cmd = args[0].ToLowerInvariant();
                if (cmd == "/install")
                {
                    DriverSetup.Uninstall();
                    DriverSetup.Install();
                } 
                else if (cmd == "/uninstall")
                {
                    DriverSetup.Uninstall();
                    return;
                }
            }


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MiApplicationContext());
        }
    }
}
