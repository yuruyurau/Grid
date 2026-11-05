using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Grid
{
    static class ShellThumb
    {
        const int CacheOnly = 0x19;
        const int Make = 0x09;

        public static Bitmap GetCached(string path, int size)
        {
            return Get(path, size, CacheOnly);
        }

        public static Bitmap Extract(string path, int size)
        {
            return Get(path, size, Make);
        }

        private static Bitmap Get(string path, int size, int flags)
        {
            IShellItemImageFactory factory = null;
            Guid iid = typeof(IShellItemImageFactory).GUID;

            int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            if (hr != 0 || factory == null)
                return null;

            try
            {
                var sz = new SIZE { cx = size, cy = size };
                IntPtr hbmp;
                hr = factory.GetImage(sz, flags, out hbmp);
                if (hr != 0 || hbmp == IntPtr.Zero)
                    return null;

                try
                {
                    using (var src = Image.FromHbitmap(hbmp))
                    {
                        return new Bitmap(src);
                    }
                }
                finally
                {
                    DeleteObject(hbmp);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (factory != null)
                    Marshal.ReleaseComObject(factory);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, int flags, out IntPtr phbm);
        }
    }
}
