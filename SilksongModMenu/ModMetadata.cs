using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace ModUINamespace
{
    [Serializable]
    public class ModMetadata
    {
        public string Guid { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public string DllFileName { get; set; } = "";  // ← 主键
        public bool Enabled { get; set; } = true;
        public string LastSeen { get; set; } = "";

        public override string ToString()
        {
            return $"{DllFileName}|{Guid}|{Name}|{Version}|{Author}|{Enabled}|{LastSeen}";
        }

        public static ModMetadata FromString(string line)
        {
            try
            {
                var parts = line.Split('|');
                if (parts.Length < 7) return null;

                return new ModMetadata
                {
                    DllFileName = parts[0],  // ← 主键
                    Guid = parts[1],
                    Name = parts[2],
                    Version = parts[3],
                    Author = parts[4],
                    Enabled = bool.Parse(parts[5]),
                    LastSeen = parts[6]
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public static class ModMetadataManager
    {
        private static readonly string MetadataFilePath = Path.Combine(
            Paths.ConfigPath,
            "SilksongModMenu_Mods.txt"
        );

        // 改用 DLL 文件名作为 Key
        private static Dictionary<string, ModMetadata> _metadata = new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        public static void Load()
        {
            if (_loaded) return;

            try
            {
                if (File.Exists(MetadataFilePath))
                {
                    var lines = File.ReadAllLines(MetadataFilePath);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var meta = ModMetadata.FromString(line);
                        if (meta != null && !string.IsNullOrEmpty(meta.DllFileName))
                        {
                            // 使用 DLL 文件名作为 Key
                            _metadata[meta.DllFileName] = meta;
                        }
                    }

                    Debug.Log($"[ModMetadata] Loaded {_metadata.Count} mods from {MetadataFilePath}");
                }
                else
                {
                    Debug.Log($"[ModMetadata] No existing metadata file, will create new one");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModMetadata] Failed to load: {ex}");
            }

            _loaded = true;
        }

        public static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# SilksongModMenu Mod Metadata");
                sb.AppendLine("# Format: DllFileName|GUID|Name|Version|Author|Enabled|LastSeen");
                sb.AppendLine();

                var list = new List<ModMetadata>(_metadata.Values);
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                foreach (var meta in list)
                {
                    sb.AppendLine(meta.ToString());
                }

                Directory.CreateDirectory(Path.GetDirectoryName(MetadataFilePath));
                File.WriteAllText(MetadataFilePath, sb.ToString());

                Debug.Log($"[ModMetadata] Saved {list.Count} mods to {MetadataFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModMetadata] Failed to save: {ex}");
            }
        }

        // 改为通过 DLL 文件名更新
        public static void UpdateMod(string dllFileName, string guid, string name, string version, string author, bool enabled)
        {
            Load();

            if (!_metadata.TryGetValue(dllFileName, out var meta))
            {
                meta = new ModMetadata { DllFileName = dllFileName };
                _metadata[dllFileName] = meta;
            }

            // 只在 GUID 有效时更新
            if (!string.IsNullOrEmpty(guid))
                meta.Guid = guid;

            meta.Name = name;
            meta.Version = version;
            meta.Author = author;
            meta.Enabled = enabled;
            meta.LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // 通过 DLL 文件名获取
        public static ModMetadata GetModByDll(string dllFileName)
        {
            Load();
            return _metadata.TryGetValue(dllFileName, out var meta) ? meta : null;
        }

        // 通过 GUID 获取（兼容旧代码）
        public static ModMetadata GetModByGuid(string guid)
        {
            Load();
            return _metadata.Values.FirstOrDefault(m => m.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
        }

        // 通过 DLL 文件名设置状态
        public static void SetEnabledByDll(string dllFileName, bool enabled)
        {
            Load();

            if (_metadata.TryGetValue(dllFileName, out var meta))
            {
                meta.Enabled = enabled;
                meta.LastSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Save();
            }
            else
            {
                Debug.LogWarning($"[ModMetadata] DLL {dllFileName} not found, creating new entry");
                UpdateMod(dllFileName, "", dllFileName, "Unknown", "Unknown", enabled);
                Save();
            }
        }

        // 通过 GUID 设置状态（兼容旧代码）
        public static void SetEnabledByGuid(string guid, bool enabled)
        {
            Load();

            var meta = GetModByGuid(guid);
            if (meta != null)
            {
                SetEnabledByDll(meta.DllFileName, enabled);
            }
            else
            {
                Debug.LogWarning($"[ModMetadata] GUID {guid} not found");
            }
        }

        public static IEnumerable<ModMetadata> GetAllMods()
        {
            Load();
            return _metadata.Values;
        }

        public static void CleanupMissing(string pluginsDir)
        {
            Load();

            var toRemove = new List<string>();

            foreach (var kvp in _metadata)
            {
                string dllFileName = kvp.Key;
                var meta = kvp.Value;

                string dllPath = Path.Combine(pluginsDir, dllFileName);
                string disabledPath = dllPath + ".disabled";

                if (!File.Exists(dllPath) && !File.Exists(disabledPath))
                {
                    if (DateTime.TryParse(meta.LastSeen, out var lastSeen))
                    {
                        if ((DateTime.Now - lastSeen).TotalDays > 7)
                        {
                            toRemove.Add(dllFileName);
                        }
                    }
                }
            }

            foreach (var dllFileName in toRemove)
            {
                _metadata.Remove(dllFileName);
                Debug.Log($"[ModMetadata] Removed missing mod: {dllFileName}");
            }

            if (toRemove.Count > 0)
            {
                Save();
            }
        }
    }
}
