using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Grid
{
    public partial class MainForm
    {

        private void Bar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        private void Edge_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            const int grip = 6;
            int hit = 0;
            if (e.X <= grip) hit = 10;
            else if (e.X >= ClientSize.Width - grip) hit = 11;
            if (e.Y <= grip) hit = hit == 10 ? 13 : hit == 11 ? 14 : 12;
            else if (e.Y >= ClientSize.Height - grip && sender == this)
                hit = hit == 10 ? 16 : hit == 11 ? 17 : 15;
            if (hit == 0) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, hit, 0);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void MainForm_MouseWheel(object sender, MouseEventArgs e)
        {
            scrollOffset -= Math.Sign(e.Delta) * CellHeight;
            ClampScroll();
            RequestVisible();
            grid.Invalidate();
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Q) { Close(); e.Handled = true; return; }
            if (e.KeyCode == Keys.R && !e.Control && !e.Shift && !e.Alt)
            {
                string preferred = (selectedIndex >= 0 && selectedIndex < filePaths.Count)
                    ? filePaths[selectedIndex]
                    : null;
                BeginLoad(currentDirectory, preferred);
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.J) { ChangeThumbSize(1); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.K) { ChangeThumbSize(-1); e.Handled = true; return; }
            if (e.KeyCode == Keys.S && e.Shift) { sortDescending = !sortDescending; ApplySort(); e.Handled = true; return; }
            if (e.KeyCode == Keys.S && !e.Control) { sortBy = (SortBy)(((int)sortBy + 1) % 5); ApplySort(); e.Handled = true; return; }

            if (e.KeyCode == Keys.G && e.Shift) { goToTopPending = 0; if (filePaths.Count > 0) Select(filePaths.Count - 1); e.Handled = true; return; }
            if (e.KeyCode == Keys.G && !e.Shift && !e.Control)
            {
                int now = Environment.TickCount;
                if (goToTopPending != 0 && unchecked(now - goToTopPending) < 800) { goToTopPending = 0; if (filePaths.Count > 0) Select(0); }
                else goToTopPending = now == 0 ? 1 : now;
                e.Handled = true;
                return;
            }
            goToTopPending = 0;

            if (filePaths.Count == 0) return;
            if (selectedIndex < 0) { Select(0); e.Handled = true; return; }

            int cols = Columns();
            switch (e.KeyCode)
            {
                case Keys.J: Select(selectedIndex + cols); break;
                case Keys.K: Select(selectedIndex - cols); break;
                case Keys.L: Select(selectedIndex + 1); break;
                case Keys.H: Select(selectedIndex - 1); break;
                case Keys.T:
                    showFileNames = !showFileNames;

                    int keep = selectedIndex;

                    FitWindow();

                    SuspendLayout();
                    PerformLayout();
                    grid.PerformLayout();
                    ResumeLayout(true);

                    Update();
                    grid.Update();

                    scrollOffset = 0;
                    if (keep >= 0 && keep < filePaths.Count)
                    {
                        Select(keep);
                    }
                    else
                    {
                        ClampScroll();
                        RequestVisible();
                    }

                    grid.Invalidate();
                    break;
                case Keys.O:
                case Keys.I:
                case Keys.Enter:
                    OpenSelected();
                    break;
            }
            e.Handled = true;
        }

        private Point mouseDownPoint;
        private int mouseDownIndex = -1;

        private void Grid_MouseDown(object sender, MouseEventArgs e)
        {
            int cols = Columns();
            int col = e.X / CellWidth;
            int row = (e.Y + scrollOffset) / CellHeight;
            if (col < 0 || col >= cols) return;
            int index = row * cols + col;
            if (index < 0 || index >= filePaths.Count) return;

            Select(index);
            if (e.Clicks == 2)
            {
                OpenSelected();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                mouseDownPoint = e.Location;
                mouseDownIndex = index;
            }
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || mouseDownIndex < 0 || mouseDownIndex >= filePaths.Count)
                return;

            int dx = Math.Abs(e.X - mouseDownPoint.X);
            int dy = Math.Abs(e.Y - mouseDownPoint.Y);

            if (dx > SystemInformation.DragSize.Width || dy > SystemInformation.DragSize.Height)
            {
                string path = filePaths[mouseDownIndex];
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { path });

                grid.DoDragDrop(data, DragDropEffects.Copy);
                mouseDownIndex = -1;
            }
        }

        private void ChangeThumbSize(int delta)
        {
            List<ThumbnailPreset> ordered = new List<ThumbnailPreset>(config.Presets);
            ordered.Sort(delegate (ThumbnailPreset a, ThumbnailPreset b)
            {
                return a.Size.CompareTo(b.Size);
            });

            int currentIdx = -1;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Size == thumbnailSize)
                {
                    currentIdx = i;
                    break;
                }
            }
            if (currentIdx < 0) currentIdx = 0;

            int nextIdx = currentIdx + delta;
            if (nextIdx < 0 || nextIdx >= ordered.Count) return;

            string keep = null;
            if (selectedIndex >= 0 && selectedIndex < filePaths.Count)
                keep = filePaths[selectedIndex];

            thumbnailSize = ordered[nextIdx].Size;
            FitWindow();
            BeginLoad(currentDirectory, keep);
        }

        private void FitWindow()
        {
            var area = Screen.FromControl(this).WorkingArea;
            var size = ClientSizeFor();

            int width = Math.Min(size.Width, area.Width);
            int height = Math.Min(size.Height, area.Height);

            int cx = Location.X + Width / 2;
            int cy = Location.Y + Height / 2;

            int x = cx - width / 2;
            int y = cy - height / 2;

            if (x + width > area.Right) x = area.Right - width;
            if (y + height > area.Bottom) y = area.Bottom - height;
            if (x < area.Left) x = area.Left;
            if (y < area.Top) y = area.Top;

            Bounds = new Rectangle(x, y, width, height);
            Update();
        }
    }
}
