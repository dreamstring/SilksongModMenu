// SilksongModMenu.Config.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;

namespace ModUINamespace
{
    public partial class SilksongModMenu
    {
        private static ConfigFile _configFile;
        private static readonly Dictionary<string, ConfigEntry<bool>> _modEnableEntries =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ModMetadata> _modMetadataCache =
            new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);

        private struct ModMetadata
        {
            public string Name;
            public string Author;
            public string Version;
            public string GUID;
        }

        private static void EnsureConfigLoaded()
        {
            try
            {
                if (_configFile != null) return;

                var configDir = Paths.ConfigPath;
                if (string.IsNullOrEmpty(configDir))
                {
                    var bepinExRoot = Path.Combine(Paths.GameRootPath, "BepInEx");
                    configDir = Path.Combine(bepinExRoot, "config");
                }

                var cfgPath = Path.Combine(configDir, "SilksongModMenu.cfg");
                _configFile = new ConfigFile(cfgPath, true);
                Logger?.LogInfo($"Config loaded: {cfgPath}");
            }
            catch (Exception e)
            {
                Logger?.LogError($"EnsureConfigLoaded error: {e}");
            }
        }

        private static bool GetModEnabled(string modKey, bool defaultValue = true)
        {
            try
            {
                EnsureConfigLoaded();
                if (string.IsNullOrWhiteSpace(modKey)) return defaultValue;

                if (_modEnableEntries.TryGetValue(modKey, out var entry))
                    return entry.Value;

                var metadata = GetModMetadata(modKey);
                string description = BuildModDescription(metadata);

                var newEntry = _configFile.Bind<bool>(
                    "Mods",
                    SanitizeKey(modKey),
                    defaultValue,
                    description
                );
                _modEnableEntries[modKey] = newEntry;
                return newEntry.Value;
            }
            catch (Exception e)
            {
                Logger?.LogError($"GetModEnabled error for '{modKey}': {e}");
                return defaultValue;
            }
        }

        private static void SetModEnabled(string modKey, bool value)
        {
            try
            {
                EnsureConfigLoaded();
                if (string.IsNullOrWhiteSpace(modKey)) return;

                if (!_modEnableEntries.TryGetValue(modKey, out var entry))
                {
                    var metadata = GetModMetadata(modKey);
                    string description = BuildModDescription(metadata);

                    entry = _configFile.Bind<bool>(
                        "Mods",
                        SanitizeKey(modKey),
                        value,
                        description
                    );
                    _modEnableEntries[modKey] = entry;
                }

                entry.Value = value;
                _configFile.Save();
                Logger?.LogInfo($"[Config] {modKey} -> {(value ? "On" : "Off")} (saved)");
            }
            catch (Exception e)
            {
                Logger?.LogError($"SetModEnabled error for '{modKey}': {e}");
            }
        }

        private static ModMetadata GetModMetadata(string modKey)
        {
            if (_modMetadataCache.TryGetValue(modKey, out var cached))
                return cached;

            foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                var pi = kv.Value;
                if (pi?.Metadata == null) continue;

                if (pi.Metadata.GUID.Equals(modKey, StringComparison.OrdinalIgnoreCase))
                {
                    var metadata = new ModMetadata
                    {
                        Name = pi.Metadata.Name,
                        Author = "Unknown",
                        Version = pi.Metadata.Version?.ToString() ?? "Unknown",
                        GUID = pi.Metadata.GUID
                    };

                    _modMetadataCache[modKey] = metadata;
                    return metadata;
                }
            }

            var fallback = new ModMetadata
            {
                Name = modKey,
                Author = "Unknown",
                Version = "Unknown",
                GUID = modKey
            };

            _modMetadataCache[modKey] = fallback;
            return fallback;
        }

        private static string BuildModDescription(ModMetadata metadata)
        {
            return $"{metadata.Name}\n" +
                   $"Author: {metadata.Author} | Version: {metadata.Version}\n" +
                   $"GUID: {metadata.GUID}\n" +
                   $"Enable/Disable this mod";
        }

        private static string SanitizeKey(string key)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return key.Replace(' ', '_');
        }

        private static void RegisterAllModsToConfig()
        {
            try
            {
                EnsureConfigLoaded();

                Logger?.LogInfo("Registering all mods to config...");

                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    var pi = kv.Value;
                    if (pi?.Metadata == null) continue;

                    if (pi.Metadata.GUID == "com.yourname.silksongmodmenu") continue;

                    var guid = pi.Metadata.GUID;
                    GetModEnabled(guid, true);
                }

                string pluginsDir = Path.Combine(Paths.BepInExRootPath, "plugins");
                if (Directory.Exists(pluginsDir))
                {
                    var disabledFiles = Directory.GetFiles(pluginsDir, "*.dll.disabled", SearchOption.TopDirectoryOnly);

                    foreach (var disabledPath in disabledFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(disabledPath);
                            string dllName = Path.GetFileNameWithoutExtension(fileName);

                            string guid = FindGuidForDllName(dllName);
                            GetModEnabled(guid, false);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning($"Failed to register disabled mod {disabledPath}: {ex.Message}");
                        }
                    }
                }

                _configFile.Save();
                Logger?.LogInfo("All mods registered to config successfully");
            }
            catch (Exception e)
            {
                Logger?.LogError($"RegisterAllModsToConfig error: {e}");
            }
        }

        private static string FindGuidForDllName(string dllName)
        {
            try
            {
                EnsureConfigLoaded();

                // 1. 从已加载的 Mod 中查找
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    var pi = kv.Value;
                    if (pi?.Location == null) continue;

                    string loadedDllName = Path.GetFileNameWithoutExtension(pi.Location);
                    if (loadedDllName.Equals(dllName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger?.LogInfo($"[GUID Lookup] Found GUID for {dllName} from loaded mods: {pi.Metadata.GUID}");
                        return pi.Metadata.GUID;
                    }
                }

                // 2. 从配置文件中查找所有已记录的 GUID
                if (_configFile != null)
                {
                    // 使用反射或直接遍历已缓存的 entries
                    foreach (var kvp in _modEnableEntries)
                    {
                        var key = kvp.Key;

                        // 策略1: GUID 包含 DLL 名称
                        if (key.IndexOf(dllName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Logger?.LogInfo($"[GUID Lookup] Found GUID for {dllName} from config (contains): {key}");
                            return key;
                        }

                        // 策略2: DLL 名称包含 GUID 的一部分
                        if (dllName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Logger?.LogInfo($"[GUID Lookup] Found GUID for {dllName} from config (reverse contains): {key}");
                            return key;
                        }

                        // 策略3: 移除特殊字符后比较
                        var cleanKey = key.Replace(".", "").Replace("_", "").Replace("-", "");
                        var cleanDll = dllName.Replace(".", "").Replace("_", "").Replace("-", "");
                        if (cleanKey.Equals(cleanDll, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger?.LogInfo($"[GUID Lookup] Found GUID for {dllName} from config (clean match): {key}");
                            return key;
                        }
                    }
                }

                // 3. 兜底：使用 DLL 文件名作为临时 GUID
                Logger?.LogWarning($"[GUID Lookup] No GUID found for {dllName}, using DLL name as fallback");
                return dllName;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"FindGuidForDllName error: {ex}");
                return dllName;
            }
        }

        private static List<string> GetAllModGuidsFromConfig()
        {
            var guids = new List<string>();
            try
            {
                EnsureConfigLoaded();

                // 直接从缓存的 entries 中获取所有 GUID
                foreach (var kvp in _modEnableEntries)
                {
                    guids.Add(kvp.Key);
                }

                Logger?.LogInfo($"Found {guids.Count} mod GUIDs in config");
            }
            catch (Exception e)
            {
                Logger?.LogError($"GetAllModGuidsFromConfig error: {e}");
            }

            return guids;
        }
    }
}
