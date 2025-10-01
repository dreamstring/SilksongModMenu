using System.Text.Json;

namespace ModPostProcessor;

internal class Config
{
    public string? ModsDir { get; set; }               // Mod 目录
    public string[] Disable { get; set; } = Array.Empty<string>(); // 要禁用的 DLL 名（不含路径），如 MyMod.dll
    public string[] Enable { get; set; } = Array.Empty<string>();  // 要启用（还原）的 DLL 名
    public string? Log { get; set; }                   // 可选：日志文件路径
    public bool Verbose { get; set; } = false;         // 详细日志
}

internal static class Program
{
    private static StreamWriter? _logWriter;

    static int Main(string[] args)
    {
        string? cfgPath = null;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? next() => (i + 1 < args.Length) ? args[++i] : null;

            switch (a)
            {
                case "--config": cfgPath = next(); break;
                case "--verbose": verbose = true; break;
                case "-v": verbose = true; break;
                default: break;
            }
        }

        if (string.IsNullOrWhiteSpace(cfgPath))
        {
            Console.WriteLine("Usage: ModPostProcessor --config \"config.json\" [--verbose]");
            return 2;
        }

        try
        {
            var cfg = LoadConfig(cfgPath);
            cfg.Verbose |= verbose;

            if (!string.IsNullOrWhiteSpace(cfg.Log))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cfg.Log)) ?? ".");
                _logWriter = new StreamWriter(new FileStream(cfg.Log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };
            }

            Log(cfg, $"[ModPostProcessor] start. modsDir='{cfg.ModsDir}'");
            if (string.IsNullOrWhiteSpace(cfg.ModsDir) || !Directory.Exists(cfg.ModsDir))
                throw new DirectoryNotFoundException($"ModsDir not found: {cfg.ModsDir}");

            int actions = 0;

            // 启用（还原 .disabled -> .dll）
            foreach (var name in cfg.Enable)
            {
                actions += EnableMod(cfg, name);
            }

            // 禁用（.dll -> .disabled）
            foreach (var name in cfg.Disable)
            {
                actions += DisableMod(cfg, name);
            }

            Log(cfg, $"done. actions={actions}");
            return 0;
        }
        catch (Exception ex)
        {
            Log(null, "Error: " + ex);
            return 1;
        }
        finally
        {
            try { _logWriter?.Dispose(); } catch { /* ignore */ }
        }
    }

    static Config LoadConfig(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        if (cfg is null) throw new InvalidOperationException("Invalid config JSON");
        return cfg;
    }

    static int DisableMod(Config cfg, string fileName)
    {
        var dllPath = Path.Combine(cfg.ModsDir!, fileName);
        var disabledPath = dllPath + ".disabled";

        if (!File.Exists(dllPath))
        {
            Log(cfg, $"[disable] skip (not found): {fileName}");
            return 0;
        }

        // 若目标存在，先备份/覆盖
        try
        {
            if (File.Exists(disabledPath))
            {
                File.Delete(disabledPath);
            }
            File.Move(dllPath, disabledPath);
            Log(cfg, $"[disable] {fileName} -> {Path.GetFileName(disabledPath)}");
            return 1;
        }
        catch (Exception ex)
        {
            Log(cfg, $"[disable] failed {fileName}: {ex.Message}");
            return 0;
        }
    }

    static int EnableMod(Config cfg, string fileName)
    {
        var dllPath = Path.Combine(cfg.ModsDir!, fileName);
        var disabledPath = dllPath + ".disabled";

        if (!File.Exists(disabledPath))
        {
            Log(cfg, $"[enable] skip (not found): {fileName}.disabled");
            return 0;
        }

        try
        {
            if (File.Exists(dllPath))
            {
                // 冲突时，优先保留 .dll，删除 .disabled
                File.Delete(disabledPath);
                Log(cfg, $"[enable] conflict: keep existing {fileName}, removed .disabled");
                return 0;
            }

            File.Move(disabledPath, dllPath);
            Log(cfg, $"[enable] {Path.GetFileName(disabledPath)} -> {fileName}");
            return 1;
        }
        catch (Exception ex)
        {
            Log(cfg, $"[enable] failed {fileName}: {ex.Message}");
            return 0;
        }
    }

    static void Log(Config? cfg, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}";
        Console.WriteLine(line);
        try { _logWriter?.WriteLine(line); } catch { /* ignore */ }
        if (cfg?.Verbose == true)
        {
            // 可扩展更详细的文件/权限检查日志
        }
    }
}
