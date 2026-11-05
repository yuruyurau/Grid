using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Grid
{
    public partial class MainForm
    {
        private class FileMeta
        {
            public DateTime CreationUtc;
            public DateTime WriteUtc;
            public long Size;
            public string Extension;
            public string FileName;
        }

        private List<FileMeta> metas = new List<FileMeta>();

        private void BeginLoad(string path, string preferred = null)
        {
            int gen = ++loadGeneration;
            path = ToWinPath(path);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Folder not found:\n" + path);
                return;
            }
            currentDirectory = path;
            Text = path;
            ClearThumbs();
            selectedIndex = -1;
            scrollOffset = 0;
            failedCount = 0;

            foreach (string file in Directory.GetFiles(path))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (!config.ImageExtensions.Contains(ext) && !config.VideoExtensions.Contains(ext)) continue;

                try
                {
                    var info = new FileInfo(file);
                    filePaths.Add(file);
                    isVideo.Add(config.VideoExtensions.Contains(ext));
                    thumbnails.Add(null);
                    metas.Add(new FileMeta
                    {
                        CreationUtc = info.CreationTimeUtc,
                        WriteUtc = info.LastWriteTimeUtc,
                        Size = info.Length,
                        Extension = ext,
                        FileName = info.Name
                    });
                }
                catch
                {
                }
            }

            SortFiles();
            RequestVisible();

            if (filePaths.Count > 0)
            {
                int idx = preferred != null ? filePaths.IndexOf(preferred) : 0;
                Select(idx >= 0 ? idx : 0);
            }
            else UpdateStatus();

            grid.Invalidate();
            StartLoader();
        }

        private void ApplySort()
        {
            loadGeneration++;
            SortFiles();
            scrollOffset = 0;
            RequestVisible();
            StartLoader();
            if (selectedIndex >= 0) Select(selectedIndex);
            else UpdateStatus();
            grid.Invalidate();
        }

        private void SortFiles()
        {
            if (filePaths.Count <= 1) return;

            var order = new int[filePaths.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, CompareFiles);

            var nf = new List<string>(filePaths.Count);
            var nt = new List<Image>(filePaths.Count);
            var nv = new List<bool>(filePaths.Count);
            var nm = new List<FileMeta>(filePaths.Count);

            string keep = selectedIndex >= 0 && selectedIndex < filePaths.Count ? filePaths[selectedIndex] : null;

            for (int i = 0; i < order.Length; i++)
            {
                int idx = order[i];
                nf.Add(filePaths[idx]);
                nt.Add(thumbnails[idx]);
                nv.Add(isVideo[idx]);
                nm.Add(metas[idx]);
            }

            filePaths.Clear(); filePaths.AddRange(nf);
            thumbnails.Clear(); thumbnails.AddRange(nt);
            isVideo.Clear(); isVideo.AddRange(nv);
            metas.Clear(); metas.AddRange(nm);

            selectedIndex = keep == null ? -1 : filePaths.IndexOf(keep);
        }

        private int CompareFiles(int a, int b)
        {
            int c = 0;
            var metaA = metas[a];
            var metaB = metas[b];

            switch (sortBy)
            {
                case SortBy.Date:
                    c = metaA.CreationUtc.CompareTo(metaB.CreationUtc);
                    break;
                case SortBy.Write:
                    c = metaA.WriteUtc.CompareTo(metaB.WriteUtc);
                    break;
                case SortBy.Size:
                    c = metaA.Size.CompareTo(metaB.Size);
                    break;
                case SortBy.Ext:
                    c = string.Compare(metaA.Extension, metaB.Extension, StringComparison.OrdinalIgnoreCase);
                    if (c == 0)
                        c = string.Compare(metaA.FileName, metaB.FileName, StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    c = string.Compare(metaA.FileName, metaB.FileName, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            if (c == 0)
                c = string.Compare(filePaths[a], filePaths[b], StringComparison.OrdinalIgnoreCase);

            return sortDescending ? -c : c;
        }

        private void Select(int index)
        {
            if (filePaths.Count == 0) return;
            if (index < 0) index = 0;
            else if (index > filePaths.Count - 1) index = filePaths.Count - 1;
            int old = selectedIndex, oldScroll = scrollOffset;
            selectedIndex = index;

            int y = (index / Columns()) * CellHeight;
            int viewH = grid.ClientSize.Height;
            if (y < scrollOffset) scrollOffset = y;
            else if (y + CellHeight > scrollOffset + viewH) scrollOffset = y + CellHeight - viewH;
            ClampScroll();
            RequestVisible();
            if (scrollOffset != oldScroll) grid.Invalidate();
            else { InvalidateCell(old); InvalidateCell(selectedIndex); }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            string mode = sortBy.ToString().ToLowerInvariant() + (sortDescending ? "-" : "+");
            string count = filePaths.Count == 0 || selectedIndex < 0
                ? "0/0"
                : string.Format("{0}/{1}", selectedIndex + 1, filePaths.Count);
            statusText = failedCount > 0
                ? string.Format("{0} {1} (fail:{2})", mode, count, failedCount)
                : string.Format("{0} {1}", mode, count);
            bar.Invalidate();
        }

        private void ClearThumbs()
        {
            foreach (var img in thumbnails)
            {
                if (img != null) img.Dispose();
            }
            thumbnails.Clear();
            filePaths.Clear();
            isVideo.Clear();
            metas.Clear();
        }

        private void StartLoader()
        {
            int gen = loadGeneration;
            int size = thumbnailSize;
            var list = filePaths.ToArray();
            var video = isVideo.ToArray();
            var thread = new Thread(delegate () { FillVisible(list, video, size, gen); }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void FillVisible(string[] list, bool[] video, int size, int gen)
        {
            while (gen == loadGeneration)
            {
                int a = 0, b = -1;
                try
                {
                    Invoke(new Action(delegate ()
                    {
                        a = visibleFirst;
                        b = visibleLast;
                    }));
                }
                catch { return; }

                bool idle = true;
                for (int i = a; i <= b && i < list.Length && gen == loadGeneration; i++)
                {
                    if (HasThumb(gen, i)) continue;
                    idle = false;

                    Image img = null;
                    string ext = Path.GetExtension(list[i]).ToLowerInvariant();
                    bool isGdiImage = !video[i] && config.GdiSupportedExtensions.Contains(ext);

                    img = ShellThumb.GetCached(list[i], size);
                    if (img != null && IsSizeInsufficient(img, size))
                    {
                        img.Dispose();
                        img = null;
                    }

                    if (img == null && isGdiImage)
                        img = ImageThumb.Get(list[i], size);

                    if (img == null)
                    {
                        img = ShellThumb.Extract(list[i], size);
                        if (img != null && isGdiImage && IsSizeInsufficient(img, size))
                        {
                            img.Dispose();
                            img = null;
                        }
                    }

                    if (img != null)
                        Publish(gen, i, img);
                    else
                    {
                        Interlocked.Increment(ref failedCount);
                        try { BeginInvoke(new Action(UpdateStatus)); }
                        catch { }
                    }
                }

                if (gen != loadGeneration) return;
                if (idle) Thread.Sleep(40);
            }
        }

        private static bool IsSizeInsufficient(Image img, int requested)
        {
            if (img == null) return true;
            int maxDim = Math.Max(img.Width, img.Height);
            return maxDim < requested * 0.75;
        }

        private bool HasThumb(int gen, int index)
        {
            try
            {
                return (bool)Invoke(new Func<bool>(delegate ()
                {
                    return gen == loadGeneration && index < thumbnails.Count && thumbnails[index] != null;
                }));
            }
            catch { return true; }
        }

        private void Publish(int gen, int index, Image img)
        {
            try
            {
                BeginInvoke(new Action(delegate ()
                {
                    if (gen != loadGeneration || index >= thumbnails.Count) { img.Dispose(); return; }
                    if (thumbnails[index] != null) thumbnails[index].Dispose();
                    thumbnails[index] = img;
                    InvalidateCell(index);
                }));
            }
            catch { img.Dispose(); }
        }

        private void OpenSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= filePaths.Count) return;
            try
            {
                Process.Start(new ProcessStartInfo(filePaths[selectedIndex]) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}
