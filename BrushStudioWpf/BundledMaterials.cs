using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace BrushStudio
{
    /// <summary>
    /// Procreate 内置材质库解析：当笔刷引用 bundledShapePath / bundledGrainPath（内置材质，
    /// zip 包里没有物理图）时，从这里解析出材质文件。
    ///  1) 优先用 exe 同目录 materials/ 下的全尺寸材质（用户可放完整材质库覆盖）；
    ///  2) 否则用内嵌的 256px 材质库（开箱即用）。
    /// </summary>
    internal static class BundledMaterials
    {
        private static readonly object Sync = new object();
        private static Dictionary<string, byte[]> embedded;
        private static bool loaded;
        private static string overrideDir;

        private static string OverrideDir
        {
            get
            {
                if (overrideDir == null)
                {
                    try
                    {
                        overrideDir = Path.Combine(AppContext.BaseDirectory, "materials");
                    }
                    catch
                    {
                        overrideDir = "";
                    }
                }
                return overrideDir;
            }
        }

        /// <summary>
        /// 解析内置材质引用：把材质写到 outputDir 下，返回文件路径。
        /// 返回 false 表示没有这个材质（调用方保持原样跳过）。
        /// </summary>
        public static bool TryExtract(string bundledPath, string outputDir, out string outPath)
        {
            outPath = null;
            if (string.IsNullOrEmpty(bundledPath))
            {
                return false;
            }
            string name = Path.GetFileName(bundledPath.Trim());
            if (name.Length == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
                string target = Path.Combine(outputDir, PathUtil.Sanitize(name));

                // 1) 全尺寸覆盖目录（exe 旁 materials/）
                string fullRes = Path.Combine(OverrideDir, name);
                if (Directory.Exists(OverrideDir) && File.Exists(fullRes))
                {
                    File.Copy(fullRes, target, true);
                    outPath = target;
                    return true;
                }

                // 2) 内嵌 256px 材质库
                byte[] data = GetEmbedded(name);
                if (data != null)
                {
                    File.WriteAllBytes(target, data);
                    outPath = target;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static byte[] GetEmbedded(string fileName)
        {
            EnsureLoaded();
            if (embedded == null)
            {
                return null;
            }
            byte[] data;
            return embedded.TryGetValue(fileName.ToLowerInvariant(), out data) ? data : null;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            lock (Sync)
            {
                if (loaded)
                {
                    return;
                }
                loaded = true;
                try
                {
                    Stream stream = OpenEmbeddedZip();
                    if (stream == null)
                    {
                        return;
                    }
                    Dictionary<string, byte[]> map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                using (Stream src = entry.Open())
                                {
                                    src.CopyTo(ms);
                                }
                                map[entry.FullName] = ms.ToArray();
                            }
                        }
                    }
                    embedded = map;
                }
                catch
                {
                    embedded = null;
                }
            }
        }

        private static Stream OpenEmbeddedZip()
        {
            Assembly asm = typeof(BundledMaterials).Assembly;
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (res.EndsWith("materials.zip", StringComparison.OrdinalIgnoreCase))
                {
                    return asm.GetManifestResourceStream(res);
                }
            }
            return null;
        }
    }
}
