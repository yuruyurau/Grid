using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Grid
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string configPath;
            string folderPath;
            ParseArgs(args, out configPath, out folderPath);
            configPath = ResolveConfigPath(configPath);

            // MessageBox.Show(
            //     "Config: " + configPath +
            //     "\nExists: " + File.Exists(configPath) +
            //     "\nExecutablePath: " + Application.ExecutablePath +
            //     "\nStartupPath: " + Application.StartupPath +
            //     "\nBaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory
            // );

            AppConfig config = LoadConfig(configPath);

            if (!string.IsNullOrEmpty(folderPath))
                config.FolderPath = MainForm.ToWinPath(folderPath);

            Application.Run(new MainForm(config));
        }

        static void ParseArgs(string[] args, out string configPath, out string folderPath)
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            configPath = Path.Combine(exeDir, "Grid.json");
            folderPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg == "--config" || arg == "-c")
                {
                    if (i + 1 >= args.Length)
                    {
                        MessageBox.Show("The --config option requires a file path.");
                        Environment.Exit(1);
                    }
                    configPath = args[++i];
                }
                else if (!arg.StartsWith("-") && folderPath == null)
                {
                    folderPath = arg;
                }
            }
        }

        static string ResolveConfigPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        static AppConfig LoadConfig(string path)
        {
            if (!File.Exists(path))
                return new AppConfig();

            try
            {
                string json = File.ReadAllText(path);
                var serializer = new JavaScriptSerializer();
                AppConfig config = serializer.Deserialize<AppConfig>(json);
                return config != null ? config : new AppConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load config file:\n" + path + "\n\n" + ex.Message + "\n\nStarting with default settings.");
                return new AppConfig();
            }
        }
    }
}
