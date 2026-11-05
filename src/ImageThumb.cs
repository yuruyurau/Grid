using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Grid
{
    static class ImageThumb
    {
        public static Bitmap Get(string path, int size)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (var ms = new MemoryStream(bytes))
                using (var src = Image.FromStream(ms, false, false))
                {
                    if (src.Width <= 0 || src.Height <= 0)
                        return null;

                    float scale = Math.Min(size / (float)src.Width, size / (float)src.Height);
                    int w = Math.Max(1, (int)(src.Width * scale));
                    int h = Math.Max(1, (int)(src.Height * scale));

                    var bmp = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
