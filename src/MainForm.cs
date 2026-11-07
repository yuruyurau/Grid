using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Grid
{
    public partial class MainForm : Form
    {
        private readonly AppConfig config;

        private class GridPanel : Panel
        {
            public GridPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                UpdateStyles();
            }
        }

        private enum SortBy { Name, Date, Write, Size, Ext }

        private readonly Panel grid;
        private readonly Panel bar;

        private string statusText = "0/0";
        private readonly List<string> filePaths = new List<string>();
        private readonly List<Image> thumbnails = new List<Image>();
        private readonly List<bool> isVideo = new List<bool>();
        private int selectedIndex = -1;
        private int scrollOffset;
        private bool showFileNames;
        private bool desiredTopMost;
        private int thumbnailSize;

        private int loadGeneration;
        private int goToTopPending;
        private int visibleFirst, visibleLast;
        private int failedCount;

        private string currentDirectory = "";
        private SortBy sortBy = SortBy.Date;
        private bool sortDescending = true;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 2;

        private int CellWidth
        {
            get { return thumbnailSize + config.CellPadding * 2; }
        }

        private int CellHeight
        {
            get { return thumbnailSize + config.CellPadding * 2 + (showFileNames ? config.FileNameHeight : 0); }
        }

        private Color BackgroundColor
        {
            get { return config.GetBackgroundColor(); }
        }

        private Color TextColor
        {
            get { return config.GetTextColor(); }
        }

        private Color SelectionColor
        {
            get { return config.GetSelectionColor(); }
        }

        private Color PlaceholderColor
        {
            get { return config.GetPlaceholderColor(); }
        }

        private Color StatusBarColor
        {
            get { return config.GetStatusBarColor(); }
        }

        private ThumbnailPreset GetCurrentPreset()
        {
            for (int i = 0; i < config.Presets.Count; i++)
            {
                if (config.Presets[i].Size == thumbnailSize)
                    return config.Presets[i];
            }
            return config.Presets.Count > 0 ? config.Presets[0] : new ThumbnailPreset { Size = 96, Columns = 8, Rows = 4 };
        }

        private int CurrentColumns
        {
            get { return GetCurrentPreset().Columns; }
        }

        private int CurrentRows
        {
            get { return GetCurrentPreset().Rows; }
        }

        public static string ToWinPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('/', '\\');
            if (path.Length >= 3 && path[0] == '\\' && char.IsLetter(path[1]) && path[2] == '\\')
                return char.ToUpperInvariant(path[1]) + ":" + path.Substring(2);
            if (path.StartsWith("\\cygdrive\\", StringComparison.OrdinalIgnoreCase) && path.Length >= 12 && char.IsLetter(path[10]))
                return char.ToUpperInvariant(path[10]) + ":" + path.Substring(11);
            return path;
        }

        public MainForm(AppConfig config)
        {
            this.config = config;

            try { Font = new Font(config.FontName, config.FontSize); }
            catch { Font = new Font("Segoe UI", config.FontSize); }

            Text = "Grid";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;

            thumbnailSize = config.ThumbnailSize;
            desiredTopMost = config.AlwaysOnTop;
            TopMost = desiredTopMost;
            showFileNames = config.ShowFileNames;
            sortDescending = config.SortDescending;
            sortBy = ParseSortBy(config.SortBy);

            ClientSize = ClientSizeFor();
            BackColor = BackgroundColor;
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            MouseDown += Edge_MouseDown;
            MouseWheel += MainForm_MouseWheel;

            bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = config.StatusBarHeight;
            bar.BackColor = StatusBarColor;
            bar.Paint += Bar_Paint;
            bar.MouseDown += Bar_MouseDown;

            grid = new GridPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = BackgroundColor;
            grid.AutoScroll = false;
            grid.TabStop = false;
            grid.Paint += Grid_Paint;
            grid.MouseDown += Grid_MouseDown;
            grid.MouseMove += Grid_MouseMove;
            grid.Resize += Grid_Resize;

            Controls.Add(grid);
            Controls.Add(bar);

            currentDirectory = string.IsNullOrEmpty(config.FolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : config.FolderPath;

            Shown += MainForm_Shown;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            BeginLoad(currentDirectory);
        }

        private void Grid_Resize(object sender, EventArgs e)
        {
            ClampScroll();
            RequestVisible();
            grid.Invalidate();
        }

        private Size ClientSizeFor()
        {
            int cellW = thumbnailSize + config.CellPadding * 2;
            int cellH = thumbnailSize + config.CellPadding * 2 + (showFileNames ? config.FileNameHeight : 0);
            return new Size(cellW * CurrentColumns, cellH * CurrentRows + config.StatusBarHeight);
        }

        private static SortBy ParseSortBy(string value)
        {
            if (string.IsNullOrEmpty(value)) return SortBy.Date;
            switch (value.ToLowerInvariant())
            {
                case "name": return SortBy.Name;
                case "write": return SortBy.Write;
                case "size": return SortBy.Size;
                case "ext": return SortBy.Ext;
                default: return SortBy.Date;
            }
        }

        private int Columns()
        {
            return Math.Max(1, grid.ClientSize.Width / CellWidth);
        }

        private int ContentH()
        {
            return filePaths.Count == 0 ? 0 : ((filePaths.Count + Columns() - 1) / Columns()) * CellHeight;
        }

        private void ClampScroll()
        {
            int max = Math.Max(0, ContentH() - grid.ClientSize.Height);
            if (scrollOffset < 0) scrollOffset = 0;
            else if (scrollOffset > max) scrollOffset = max;
        }

        private void RequestVisible()
        {
            if (filePaths.Count == 0) { visibleFirst = 0; visibleLast = -1; return; }
            int cols = Columns();
            int firstRow = scrollOffset / CellHeight;
            int viewRows = Math.Max(1, (grid.ClientSize.Height + CellHeight - 1) / CellHeight);
            visibleFirst = Math.Max(0, (firstRow - 1) * cols);
            visibleLast = Math.Min(filePaths.Count - 1, (firstRow + viewRows) * cols - 1);
        }

        private void InvalidateCell(int index)
        {
            if (index < 0 || index >= filePaths.Count) return;
            int cols = Columns();
            grid.Invalidate(new Rectangle((index % cols) * CellWidth, (index / cols) * CellHeight - scrollOffset, CellWidth, CellHeight));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            loadGeneration++;
            ClearThumbs();
            base.OnFormClosed(e);
        }
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (desiredTopMost)
                ScheduleRestoreTopMost();
        }
    }
}
