using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Grid
{
    public partial class MainForm
    {
        private void Bar_Paint(object sender, PaintEventArgs e)
        {
            string leftPart = "";
            string rightPart = statusText;

            int spaceIndex = statusText.IndexOf(' ');
            if (spaceIndex > 0)
            {
                leftPart = statusText.Substring(0, spaceIndex);
                rightPart = statusText.Substring(spaceIndex + 1);
            }

            int leftWidth = TextRenderer.MeasureText(leftPart, Font).Width + 8;

            var leftRect = new Rectangle(4, 0, leftWidth, bar.Height);
            TextRenderer.DrawText(e.Graphics, leftPart, Font, leftRect, TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            var rightRect = new Rectangle(leftWidth + 4, 0, bar.Width - leftWidth - 4, bar.Height);
            TextRenderer.DrawText(e.Graphics, rightPart, Font, rightRect, TextColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        private void Grid_Paint(object sender, PaintEventArgs e)
        {
            int cols = Columns();
            int viewTop = scrollOffset;
            int viewBottom = scrollOffset + grid.ClientSize.Height;

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            for (int i = 0; i < filePaths.Count; i++)
            {
                int x = (i % cols) * CellWidth;
                int y = (i / cols) * CellHeight;
                if (y + CellHeight < viewTop || y > viewBottom) continue;

                var cell = new Rectangle(x, y - scrollOffset, CellWidth, CellHeight);

                using (var brush = new SolidBrush(BackgroundColor))
                    e.Graphics.FillRectangle(brush, cell);

                if (i == selectedIndex)
                {
                    string style = (config.SelectionStyle ?? "fill").ToLowerInvariant();

                    if (style == "fill")
                    {
                        using (var brush = new SolidBrush(SelectionColor))
                            e.Graphics.FillRectangle(brush, cell);
                    }
                    else if (style == "border")
                    {
                        using (var pen = new Pen(SelectionColor, 2))
                            e.Graphics.DrawRectangle(pen, cell.X + 1, cell.Y + 1, cell.Width - 3, cell.Height - 3);
                    }
                    else if (style == "bracket")
                    {
                        int len = Math.Max(8, thumbnailSize / 8);
                        int thick = 2;

                        using (var pen = new Pen(SelectionColor, thick))
                        {
                            // 左上
                            e.Graphics.DrawLine(pen, cell.X + 1, cell.Y + 1, cell.X + 1 + len, cell.Y + 1);
                            e.Graphics.DrawLine(pen, cell.X + 1, cell.Y + 1, cell.X + 1, cell.Y + 1 + len);
                            // 右上
                            e.Graphics.DrawLine(pen, cell.Right - 1, cell.Y + 1, cell.Right - 1 - len, cell.Y + 1);
                            e.Graphics.DrawLine(pen, cell.Right - 1, cell.Y + 1, cell.Right - 1, cell.Y + 1 + len);
                            // 左下
                            e.Graphics.DrawLine(pen, cell.X + 1, cell.Bottom - 1, cell.X + 1 + len, cell.Bottom - 1);
                            e.Graphics.DrawLine(pen, cell.X + 1, cell.Bottom - 1, cell.X + 1, cell.Bottom - 1 - len);
                            // 右下
                            e.Graphics.DrawLine(pen, cell.Right - 1, cell.Bottom - 1, cell.Right - 1 - len, cell.Bottom - 1);
                            e.Graphics.DrawLine(pen, cell.Right - 1, cell.Bottom - 1, cell.Right - 1, cell.Bottom - 1 - len);
                        }
                    }
                }

                var box = new Rectangle(
                    cell.X + config.CellPadding,
                    cell.Y + config.CellPadding,
                    thumbnailSize,
                    thumbnailSize);

                if (i < thumbnails.Count && thumbnails[i] != null)
                {
                    e.Graphics.DrawImage(thumbnails[i], FitRect(thumbnails[i].Width, thumbnails[i].Height, box));
                }
                else
                {
                    DrawPlaceholder(e.Graphics, box);
                }

                if (showFileNames)
                {
                    var textRect = new Rectangle(
                        cell.X + 2,
                        cell.Y + config.CellPadding + thumbnailSize,
                        cell.Width - 4,
                        config.FileNameHeight);

                    TextRenderer.DrawText(e.Graphics, Path.GetFileName(filePaths[i]), Font, textRect, TextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
            }
        }

        private void DrawPlaceholder(Graphics g, Rectangle box)
        {
            float s = Math.Max(0.1f, thumbnailSize / 96f);
            using (Brush b = new SolidBrush(PlaceholderColor))
                g.FillRectangle(b, box.X + 16 * s, box.Y + 28 * s, 64 * s, 40 * s);
            using (Brush b = new SolidBrush(BackgroundColor))
                g.FillPolygon(b, new PointF[] {
                    new PointF(box.X + 40 * s, box.Y + 38 * s),
                    new PointF(box.X + 40 * s, box.Y + 58 * s),
                    new PointF(box.X + 58 * s, box.Y + 48 * s)
                });
        }

        private static Rectangle FitRect(int srcW, int srcH, Rectangle box)
        {
            if (srcW <= 0 || srcH <= 0) return box;
            float scale = Math.Min(box.Width / (float)srcW, box.Height / (float)srcH);
            int w = Math.Max(1, (int)(srcW * scale));
            int h = Math.Max(1, (int)(srcH * scale));
            return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }
    }
}
