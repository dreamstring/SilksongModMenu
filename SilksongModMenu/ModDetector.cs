using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace SilksongModManagerMod
{
    public class ModDetector
    {
        private List<ModInfo> detectedMods;
        private string pluginsPath;

        public List<ModInfo> DetectedMods => detectedMods ?? new List<ModInfo>();

        public ModDetector()
        {
            detectedMods = new List<ModInfo>();
            pluginsPath = Path.Combine(Paths.GameRootPath, "BepInEx", "plugins");
            SilksongModMenu.Logger.LogInfo("插件目录路径: " + pluginsPath);
            SilksongModMenu.Logger.LogInfo("ModDetector 已初始化，扫描将在打开UI时触发");
        }

        public void RefreshModList()
        {
            try
            {
                detectedMods.Clear();
                if (!Directory.Exists(pluginsPath))
                {
                    SilksongModMenu.Logger.LogWarning("插件目录不存在: " + pluginsPath);
                    return;
                }

                SilksongModMenu.Logger.LogInfo("刷新Mod列表");

                // 扫描BepInEx插件目录
                var pluginFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.AllDirectories))
                    .ToArray();

                foreach (string filePath in pluginFiles)
                {
                    try
                    {
                        var modInfo = new ModInfo
                        {
                            FilePath = filePath,
                            FileName = Path.GetFileName(filePath),
                            FileSize = new FileInfo(filePath).Length,
                            LastModified = File.GetLastWriteTime(filePath),
                            IsEnabled = !filePath.EndsWith(".disabled"),
                            IsLoaded = false,
                            DisplayName = Path.GetFileNameWithoutExtension(filePath.Replace(".disabled", "")),
                            GUID = "N/A",
                            Version = "1.0.0"
                        };

                        detectedMods.Add(modInfo);
                        SilksongModMenu.Logger.LogInfo($"Found mod: {modInfo.FileName} (Enabled: {modInfo.IsEnabled})");
                    }
                    catch (Exception ex)
                    {
                        SilksongModMenu.Logger.LogError($"Error processing mod file {filePath}: {ex.Message}");
                    }
                }

                SilksongModMenu.Logger.LogInfo($"Total mods found: {detectedMods.Count}");
            }
            catch (Exception ex)
            {
                SilksongModMenu.Logger.LogError("刷新模组列表失败: " + ex.Message);
            }
        }
    }

    public class ModInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsLoaded { get; set; }
        public string DisplayName { get; set; }
        public string GUID { get; set; }
        public string Version { get; set; }
    }
}