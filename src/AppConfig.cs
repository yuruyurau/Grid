using System;
using System.Collections.Generic;
using System.Drawing;

namespace Grid
{
    public class ThumbnailPreset
    {
        public int Size { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
    }

    public class AppConfig
    {
        public string FolderPath { get; set; }

        public int ThumbnailSize { get; set; }

        public bool AlwaysOnTop { get; set; }

        public bool ShowFileNames { get; set; }

        // "fill" / "bracket" / "border"
        public string SelectionStyle { get; set; }

        /// Sort key: name / date / write / size / ext
        public string SortBy { get; set; }

        public bool SortDescending { get; set; }

        public string FontName { get; set; }
        public float FontSize { get; set; }

        public string BackgroundColor { get; set; }
        public string TextColor { get; set; }
        public string SelectionColor { get; set; }
        public string PlaceholderColor { get; set; }
        public string StatusBarColor { get; set; }

        public int FileNameHeight { get; set; }
        public int CellPadding { get; set; }
        public int StatusBarHeight { get; set; }

        public List<ThumbnailPreset> Presets { get; set; }

        public List<string> ImageExtensions { get; set; }
        public List<string> GdiSupportedExtensions { get; set; }
        public List<string> VideoExtensions { get; set; }

        public AppConfig()
        {
            FolderPath = "";
            ThumbnailSize = 96;
            AlwaysOnTop = false;
            ShowFileNames = false;
            SelectionStyle = "fill";
            SortBy = "date";
            SortDescending = true;

            FontName = "Cascadia Mono";
            FontSize = 9f;

            BackgroundColor = "#1F1F28";
            TextColor = "#DCD7BA";
            SelectionColor = "#505053";
            PlaceholderColor = "#2E2E2E";
            StatusBarColor = "#505053";

            FileNameHeight = 18;
            CellPadding = 6;
            StatusBarHeight = 22;

            Presets = new List<ThumbnailPreset>
            {
                new ThumbnailPreset { Size = 16,  Columns = 16, Rows = 10 },
                new ThumbnailPreset { Size = 48,  Columns = 16, Rows = 7  },
                new ThumbnailPreset { Size = 96,  Columns = 8,  Rows = 4  },
                new ThumbnailPreset { Size = 256, Columns = 3,  Rows = 2  },
            };

            ImageExtensions = new List<string>
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico"
            };

            GdiSupportedExtensions = new List<string>
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".ico"
            };

            VideoExtensions = new List<string>
            {
                ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".flv", ".mpeg", ".mpg"
            };

        }

        public Color GetBackgroundColor() { return ParseColor(BackgroundColor, Color.FromArgb(31, 31, 40)); }
        public Color GetTextColor() { return ParseColor(TextColor, Color.FromArgb(220, 215, 186)); }
        public Color GetSelectionColor() { return ParseColor(SelectionColor, Color.FromArgb(80, 80, 83)); }
        public Color GetPlaceholderColor() { return ParseColor(PlaceholderColor, Color.FromArgb(46, 46, 46)); }
        public Color GetStatusBarColor() { return ParseColor(StatusBarColor, Color.FromArgb(80, 80, 83)); }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;

            try
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);

                if (hex.Length == 6)
                {
                    return Color.FromArgb(
                        Convert.ToInt32(hex.Substring(0, 2), 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16));
                }
                else if (hex.Length == 8)
                {
                    return Color.FromArgb(
                        Convert.ToInt32(hex.Substring(0, 2), 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16),
                        Convert.ToInt32(hex.Substring(6, 2), 16));
                }
                else
                {
                    return fallback;
                }
            }
            catch
            {
                return fallback;
            }
        }
    }
}
