using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
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
            configPath = Path.Combine(exeDir, "Grid.toml");
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
                var config = new AppConfig();

                string[] lines = File.ReadAllLines(path);
                ThumbnailPreset currentPreset = null;
                bool inPresets = false;
                bool hasPresets = false;

                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    if (line == "[[Presets]]")
                    {
                        if (!hasPresets)
                        {
                            config.Presets.Clear();
                            hasPresets = true;
                        }
                        currentPreset = new ThumbnailPreset();
                        config.Presets.Add(currentPreset);
                        inPresets = true;
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (inPresets && currentPreset != null)
                    {
                        switch (key)
                        {
                            case "Size": currentPreset.Size = int.Parse(value); break;
                            case "Columns": currentPreset.Columns = int.Parse(value); break;
                            case "Rows": currentPreset.Rows = int.Parse(value); break;
                        }
                        continue;
                    }

                    switch (key)
                    {
                        case "FolderPath":
                            config.FolderPath = Unquote(value);
                            break;
                        case "ThumbnailSize":
                            config.ThumbnailSize = int.Parse(value);
                            break;
                        case "SelectionStyle":
                            config.SelectionStyle = Unquote(value).ToLowerInvariant();
                            break;
                        case "ShowFileNames":
                            config.ShowFileNames = ParseBool(value);
                            break;
                        case "SortBy":
                            config.SortBy = Unquote(value);
                            break;
                        case "SortDescending":
                            config.SortDescending = ParseBool(value);
                            break;
                        case "FontName":
                            config.FontName = Unquote(value);
                            break;
                        case "FontSize":
                            config.FontSize = float.Parse(value);
                            break;
                        case "BackgroundColor":
                            config.BackgroundColor = Unquote(value);
                            break;
                        case "TextColor":
                            config.TextColor = Unquote(value);
                            break;
                        case "SelectionColor":
                            config.SelectionColor = Unquote(value);
                            break;
                        case "PlaceholderColor":
                            config.PlaceholderColor = Unquote(value);
                            break;
                        case "StatusBarColor":
                            config.StatusBarColor = Unquote(value);
                            break;
                        case "FileNameHeight":
                            config.FileNameHeight = int.Parse(value);
                            break;
                        case "CellPadding":
                            config.CellPadding = int.Parse(value);
                            break;
                        case "StatusBarHeight":
                            config.StatusBarHeight = int.Parse(value);
                            break;
                        case "ImageExtensions":
                            config.ImageExtensions = ParseStringArray(value);
                            break;
                        case "GdiSupportedExtensions":
                            config.GdiSupportedExtensions = ParseStringArray(value);
                            break;
                        case "VideoExtensions":
                            config.VideoExtensions = ParseStringArray(value);
                            break;
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load config:\n" + path + "\n\n" + ex.Message + "\n\nStarting with default settings.");
                return new AppConfig();
            }
        }

        static string Unquote(string s)
        {
            if (s.Length >= 2 && ((s[0] == '"' && s[s.Length - 1] == '"') || (s[0] == '\'' && s[s.Length - 1] == '\'')))
                return s.Substring(1, s.Length - 2);
            return s;
        }

        static bool ParseBool(string value)
        {
            value = value.Trim().ToLowerInvariant();
            return value == "true" || value == "1" || value == "yes";
        }

        static List<string> ParseStringArray(string value)
        {
            var list = new List<string>();
            value = value.Trim();
            if (value.StartsWith("[") && value.EndsWith("]"))
                value = value.Substring(1, value.Length - 2);

            foreach (string part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                list.Add(Unquote(trimmed));
            }
            return list;
        }
    }
}
