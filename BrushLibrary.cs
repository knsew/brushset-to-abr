using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BrushStudio
{
    /// <summary>单支笔刷的个性化覆盖（改名/反转/收藏）。</summary>
    internal sealed class LibraryEntry
    {
        public string Name;          // 改名（null = 用原名）
        public bool? Invert;         // 单笔刷反转 alpha（null = 跟随全局）
        public bool Favorite;        // 是否收藏
    }

    /// <summary>
    /// 笔刷库：按 (笔刷包路径, 笔刷 sourceId) 持久化每支笔刷的 改名/反转/收藏。
    /// 文件：%AppData%\BrushsetToABR\brush-library.txt（UTF-8，行式 \x1F 分隔）。
    /// </summary>
    internal static class BrushLibrary
    {
        private const char Sep = '\x1F';
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrushsetToABR", "brush-library.txt");

        private static readonly Dictionary<string, LibraryEntry> Entries =
            new Dictionary<string, LibraryEntry>(StringComparer.Ordinal);

        static BrushLibrary()
        {
            Load();
        }

        public static string KeyFor(string brushsetPath, string sourceId)
        {
            return (brushsetPath ?? "") + Sep + (sourceId ?? "");
        }

        /// <summary>取某支笔刷的覆盖；无则返回 null。</summary>
        public static LibraryEntry Get(string brushsetPath, string sourceId)
        {
            LibraryEntry entry;
            if (Entries.TryGetValue(KeyFor(brushsetPath, sourceId), out entry))
            {
                return entry;
            }
            return null;
        }

        public static void SetName(string brushsetPath, string sourceId, string name)
        {
            LibraryEntry e = Ensure(brushsetPath, sourceId);
            e.Name = string.IsNullOrEmpty(name) ? null : name;
            Save();
        }

        public static void SetInvert(string brushsetPath, string sourceId, bool? invert)
        {
            LibraryEntry e = Ensure(brushsetPath, sourceId);
            e.Invert = invert;
            Save();
        }

        public static void SetFavorite(string brushsetPath, string sourceId, bool favorite)
        {
            LibraryEntry e = Ensure(brushsetPath, sourceId);
            e.Favorite = favorite;
            Save();
        }

        public static bool IsFavorite(string brushsetPath, string sourceId)
        {
            LibraryEntry e = Get(brushsetPath, sourceId);
            return e != null && e.Favorite;
        }

        private static LibraryEntry Ensure(string brushsetPath, string sourceId)
        {
            string key = KeyFor(brushsetPath, sourceId);
            LibraryEntry entry;
            if (!Entries.TryGetValue(key, out entry))
            {
                entry = new LibraryEntry();
                Entries[key] = entry;
            }
            return entry;
        }

        private static void Load()
        {
            Entries.Clear();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    string[] parts = line.Split(Sep);
                    if (parts.Length < 2)
                    {
                        continue;
                    }
                    LibraryEntry e = new LibraryEntry();
                    if (parts.Length > 2 && parts[2].Length > 0)
                    {
                        e.Name = parts[2];
                    }
                    if (parts.Length > 3 && parts[3].Length > 0)
                    {
                        e.Invert = parts[3] == "1";
                    }
                    if (parts.Length > 4 && parts[4] == "1")
                    {
                        e.Favorite = true;
                    }
                    Entries[parts[0] + Sep + parts[1]] = e;
                }
            }
            catch
            {
                // 读取失败则忽略（下次保存重建）
            }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, LibraryEntry> pair in Entries)
                {
                    string[] keyParts = pair.Key.Split(Sep);
                    if (keyParts.Length != 2)
                    {
                        continue;
                    }
                    LibraryEntry e = pair.Value;
                    sb.Append(keyParts[0]).Append(Sep).Append(keyParts[1]).Append(Sep)
                      .Append(e.Name ?? "").Append(Sep)
                      .Append(e.Invert.HasValue ? (e.Invert.Value ? "1" : "0") : "").Append(Sep)
                      .Append(e.Favorite ? "1" : "0").Append('\n');
                }
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
