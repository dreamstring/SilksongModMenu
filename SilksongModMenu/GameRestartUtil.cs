using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

public static class GameRestartUtil
{
    private const string UpdaterFileName = "RestartStub.exe";
    private const string PostProcessorFileName = "ModPostProcessor.exe";

    private const string JsonConfigRelativePath = "BepInEx/config/mod_processor_config.json";
    private const string LogRelativePath = "BepInEx/logs/mod_processor.log";
    private const string RestartStubLogRelativePath = "BepInEx/logs/restart_stub.log";

    private static bool _modChangesProcessed = false;

    public static void RestartGameImmediate()
    {
#if UNITY_EDITOR
        UnityEngine.Debug.LogWarning("RestartGameImmediate called in Editor - 仅退出播放模式，不会真正重启进程。");
        UnityEditor.EditorApplication.isPlaying = false;
        return;
#else
        try
        {
            string exePath = GetGameExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                UnityEngine.Debug.LogError("Restart failed: cannot resolve executable path.");
                return;
            }

            string exeDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(exeDir))
            {
                UnityEngine.Debug.LogError("Restart failed: cannot resolve executable directory.");
                return;
            }

            ProcessModChanges(exeDir);
            _modChangesProcessed = true;

            string steamAppId = GetSteamAppId(exeDir);
            if (string.IsNullOrEmpty(steamAppId))
            {
                UnityEngine.Debug.LogError("Restart failed: steam_appid.txt not found.");
                return;
            }

            string updaterPath = Path.Combine(exeDir, "BepInEx", "plugins", UpdaterFileName);
            if (!File.Exists(updaterPath))
            {
                UnityEngine.Debug.LogError("Restart failed: RestartStub not found at " + updaterPath);
                return;
            }

            string steamUrl = "steam://rungameid/" + steamAppId;
            int currentPid = Process.GetCurrentProcess().Id;
            string logPath = Path.Combine(exeDir, RestartStubLogRelativePath);
            EnsureDirectoryExists(logPath);  // 添加这

            var argBuilder = new StringBuilder();
            argBuilder.Append("--target \"").Append(steamUrl).Append("\" ");
            argBuilder.Append("--waitPid ").Append(currentPid).Append(" ");
            argBuilder.Append("--log \"").Append(logPath).Append("\" ");
            argBuilder.Append("--delayMs 2000 ");
            argBuilder.Append("--verbose");

            string updaterArgs = argBuilder.ToString().Trim();

            var psi = new ProcessStartInfo();
            psi.FileName = updaterPath;
            psi.Arguments = updaterArgs;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = exeDir;
            psi.CreateNoWindow = true;

            UnityEngine.Debug.Log("=== RESTART VIA STEAM PROTOCOL ===");
            UnityEngine.Debug.Log("Steam AppId: " + steamAppId);
            UnityEngine.Debug.Log("Steam URL: " + steamUrl);
            UnityEngine.Debug.Log("RestartStub: " + psi.FileName);
            UnityEngine.Debug.Log("Arguments: " + psi.Arguments);

            var started = Process.Start(psi);
            if (started == null)
            {
                UnityEngine.Debug.LogError("Restart failed: cannot start RestartStub.");
                return;
            }

            UnityEngine.Debug.Log("RestartStub started (PID=" + started.Id + "), exiting game...");

            System.Threading.Thread.Sleep(1000);
            Application.Quit();
            System.Threading.Thread.Sleep(500);

            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception killEx)
            {
                UnityEngine.Debug.LogWarning("Process.Kill fallback failed: " + killEx);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Restart failed: " + e);
        }
#endif
    }

    public static void ProcessModChangesOnExit()
    {
        if (_modChangesProcessed)
        {
            UnityEngine.Debug.Log("[Exit Hook] Mod changes already processed during restart, skipping.");
            return;
        }

        try
        {
            string exePath = GetGameExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                UnityEngine.Debug.LogWarning("[Exit Hook] Cannot resolve executable path.");
                return;
            }

            string exeDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(exeDir))
            {
                UnityEngine.Debug.LogWarning("[Exit Hook] Cannot resolve executable directory.");
                return;
            }

            ProcessModChanges(exeDir);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[Exit Hook] Failed to process mod changes: " + ex);
        }
    }

    private static void ProcessModChanges(string gameDir)
    {
        try
        {
            string pluginsDir = Path.Combine(gameDir, "BepInEx", "plugins");
            string postProcessorPath = Path.Combine(pluginsDir, PostProcessorFileName);

            if (!File.Exists(postProcessorPath))
            {
                UnityEngine.Debug.LogWarning("ModPostProcessor.exe not found, skipping mod changes.");
                return;
            }

            // 加载元数据
            ModUINamespace.ModMetadataManager.Load();

            var modStates = GetModStatesFromMetadata();
            if (modStates.Count == 0)
            {
                UnityEngine.Debug.Log("No mod state changes detected.");
                return;
            }

            UnityEngine.Debug.Log($"=== MOD STATES ({modStates.Count} mods) ===");
            foreach (var kvp in modStates)
            {
                UnityEngine.Debug.Log($"  {kvp.Key} → {(kvp.Value ? "Enabled" : "Disabled")}");
            }

            string jsonConfigPath = Path.Combine(gameDir, JsonConfigRelativePath);
            string logPath = Path.Combine(gameDir, LogRelativePath);
            EnsureDirectoryExists(jsonConfigPath);
            EnsureDirectoryExists(logPath);
            GenerateJsonConfig(jsonConfigPath, pluginsDir, modStates, logPath);

            var psi = new ProcessStartInfo();
            psi.FileName = postProcessorPath;
            psi.Arguments = $"--config \"{jsonConfigPath}\" --verbose";
            psi.UseShellExecute = false;
            psi.WorkingDirectory = gameDir;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            UnityEngine.Debug.Log("=== PROCESSING MOD CHANGES ===");
            UnityEngine.Debug.Log("Config: " + jsonConfigPath);
            UnityEngine.Debug.Log("Command: " + psi.FileName + " " + psi.Arguments);

            var proc = Process.Start(psi);
            if (proc == null)
            {
                UnityEngine.Debug.LogError("Failed to start ModPostProcessor.");
                return;
            }

            int timeoutMs = 10000;
            if (!proc.WaitForExit(timeoutMs))
            {
                UnityEngine.Debug.LogWarning($"ModPostProcessor timeout after {timeoutMs}ms, killing process.");
                try { proc.Kill(); } catch { }
            }

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(output))
                UnityEngine.Debug.Log("ModPostProcessor output:\n" + output);

            if (!string.IsNullOrEmpty(error))
                UnityEngine.Debug.LogError("ModPostProcessor error:\n" + error);

            UnityEngine.Debug.Log("ModPostProcessor exit code: " + proc.ExitCode);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("Failed to process mod changes: " + ex);
        }
    }

    // 从元数据获取 Mod 状态
    private static Dictionary<string, bool> GetModStatesFromMetadata()
    {
        var modStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var meta in ModUINamespace.ModMetadataManager.GetAllMods())
            {
                // ========== 使用 SilksongModMenu 的保护列表 ==========
                if (ModUINamespace.SilksongModMenu.IgnoredDlls.Contains(meta.DllFileName))
                {
                    UnityEngine.Debug.Log($"[Protected] Skipping {meta.DllFileName}");
                    continue;
                }
                // ===================================================

                // 使用 DLL 文件名作为 Key
                modStates[meta.DllFileName] = meta.Enabled;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("Failed to get mod states from metadata: " + ex);
        }

        return modStates;
    }

    // 生成 JSON 配置（使用元数据）
    private static void GenerateJsonConfig(
        string jsonPath,
        string pluginsDir,
        Dictionary<string, bool> modStates,
        string logPath)
    {
        var toEnable = new List<string>();
        var toDisable = new List<string>();

        UnityEngine.Debug.Log("=== Generating JSON Config ===");
        UnityEngine.Debug.Log($"Total mods in metadata: {modStates.Count}");

        foreach (var kvp in modStates)
        {
            string dllFileName = kvp.Key;
            bool targetEnabled = kvp.Value;

            // 保护系统 DLL
            if (ModUINamespace.SilksongModMenu.IgnoredDlls.Contains(dllFileName))
            {
                UnityEngine.Debug.Log($"[Protected] {dllFileName}");
                continue;
            }

            // 按目标状态分类（不检查当前状态，让 ModPostProcessor 处理）
            if (targetEnabled)
            {
                toEnable.Add(dllFileName);
            }
            else
            {
                toDisable.Add(dllFileName);
            }

            var meta = ModUINamespace.ModMetadataManager.GetModByDll(dllFileName);
            string displayName = meta?.Name ?? dllFileName;
            UnityEngine.Debug.Log($"  {displayName} → {(targetEnabled ? "ENABLE" : "DISABLE")}");
        }

        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"ModsDir\": \"{EscapeJson(pluginsDir)}\",");
        json.AppendLine($"  \"Enable\": [{string.Join(", ", toEnable.Select(s => $"\"{EscapeJson(s)}\""))}],");
        json.AppendLine($"  \"Disable\": [{string.Join(", ", toDisable.Select(s => $"\"{EscapeJson(s)}\""))}],");
        json.AppendLine($"  \"Log\": \"{EscapeJson(logPath)}\",");
        json.AppendLine("  \"Verbose\": true");
        json.AppendLine("}");

        File.WriteAllText(jsonPath, json.ToString());

        UnityEngine.Debug.Log("=== JSON Config Summary ===");
        UnityEngine.Debug.Log($"Enable: {toEnable.Count} mods");
        UnityEngine.Debug.Log($"Disable: {toDisable.Count} mods");
        UnityEngine.Debug.Log($"Config saved to: {jsonPath}");
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetGameExecutablePath()
    {
        try
        {
            var module = Process.GetCurrentProcess().MainModule;
            if (module != null && !string.IsNullOrEmpty(module.FileName))
                return module.FileName;
        }
        catch { }

        string candidate = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Application.productName + ".exe"));
        if (File.Exists(candidate))
            return candidate;

        return null;
    }

    private static string GetSteamAppId(string gameDir)
    {
        try
        {
            string steamAppIdFile = Path.Combine(gameDir, "steam_appid.txt");
            if (File.Exists(steamAppIdFile))
            {
                string content = File.ReadAllText(steamAppIdFile).Trim();
                content = content.Replace("\uFEFF", "").Trim();
                if (!string.IsNullOrEmpty(content) && int.TryParse(content, out _))
                {
                    return content;
                }
            }

            string envAppId = Environment.GetEnvironmentVariable("SteamAppId");
            if (!string.IsNullOrEmpty(envAppId))
                return envAppId;

            return "2145240";
        }
        catch
        {
            return "2145240";
        }
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        try
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Failed to create directory for {filePath}: {ex.Message}");
        }
    }
}

