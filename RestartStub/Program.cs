using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RestartStub;

internal class Options
{
    public string? Target;
    public string? Args;
    public int? WaitPid;
    public string? Post;
    public string? PostArgs;
    public int DelayMs = 1200;
    public int PostTimeoutMs = 30000;
    public bool Verbose = false;
    public string? WorkDir;
    public string? LogPath;
    public bool AutoSteamFallback = false;
    public string? SteamAppId;
    public int ChildProcessCheckDelayMs = 3000;
}

internal static class Program
{
    private static StreamWriter? _logWriter;

    static int Main(string[] args)
    {
        var opt = Parse(args);
        if (opt is null || string.IsNullOrWhiteSpace(opt.Target))
        {
            Console.WriteLine("Usage: RestartStub --target \"game.exe|steam://rungameid/XXXXXX\" [options]");
            return 2;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(opt.LogPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opt.LogPath)) ?? ".");
                _logWriter = new StreamWriter(new FileStream(opt.LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };
            }

            bool isSteamProtocol = IsSteamProtocol(opt.Target!);
            string? targetFull = isSteamProtocol ? null : GetFullPathSafe(opt.Target!);
            string workDir = isSteamProtocol
                ? Environment.CurrentDirectory
                : ResolveWorkDir(opt, targetFull);

            if (!isSteamProtocol && (targetFull is null || !File.Exists(targetFull)))
            {
                Log(opt, $"Error: target not found -> '{opt.Target}'");
                TrySteamFallbackAndHint(opt);
                return 2;
            }

            Log(opt, $"[RestartStub] OS={RuntimeInformation.OSDescription}, target='{opt.Target}'");

            // 1) 等待 PID
            if (opt.WaitPid is int pid && pid > 0)
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    Log(opt, $"Waiting PID {pid}...");
                    p.WaitForExit();
                }
                catch { Log(opt, $"PID {pid} not found."); }
            }

            // 2) 延迟
            if (opt.DelayMs > 0)
            {
                Log(opt, $"Delay {opt.DelayMs} ms...");
                Thread.Sleep(opt.DelayMs);
            }

            // 3) 后处理器
            if (!string.IsNullOrWhiteSpace(opt.Post))
            {
                RunChild(opt, opt.Post!, opt.PostArgs, opt.PostTimeoutMs);
            }

            // 4) Doorstop 检查（仅非 Steam）
            if (!isSteamProtocol)
            {
                try { Directory.SetCurrentDirectory(workDir); } catch { }

                var check = DoorstopCheck(workDir);
                Log(opt, $"[Check] Doorstop: {check.ProxyExists}, config: {check.ConfigExists}, BepInEx: {check.BepinDirExists}");

                if (!check.BepinDirExists || !check.ConfigExists || !check.ProxyExists)
                {
                    Log(opt, "[Hint] Doorstop 不完整，可能不会注入。");
                    if (TrySteamFallbackAndHint(opt)) return 0;
                }
            }

            // 5) 启动目标
            Process? proc;

            if (isSteamProtocol)
            {
                proc = StartViaSteam(opt.Target!);
            }
            else
            {
                proc = StartViaExe(opt, targetFull!, workDir);
            }

            if (proc is null)
            {
                Log(opt, "Failed to start.");
                if (TrySteamFallbackAndHint(opt)) return 0;
                return 3;
            }

            Log(opt, $"Started. PID={proc.Id}");

            // 6) 子进程检测（仅 Windows + 非 Steam）
            if (!isSteamProtocol && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && opt.ChildProcessCheckDelayMs > 0)
            {
                Thread.Sleep(opt.ChildProcessCheckDelayMs);
                if (!proc.HasExited)
                {
                    var children = GetChildProcessesWindows(proc.Id);
                    if (children.Count > 0)
                    {
                        Log(opt, $"[Warning] Detected {children.Count} child process(es).");
                        if (opt.AutoSteamFallback && !string.IsNullOrWhiteSpace(opt.SteamAppId))
                        {
                            Log(opt, "[AutoFallback] Killing and restarting via Steam...");
                            try
                            {
                                proc.Kill(true);
                                foreach (var c in children) try { c.Kill(true); } catch { }
                            }
                            catch { }
                            Thread.Sleep(1000);
                            return TrySteamFallbackAndHint(opt) ? 0 : 3;
                        }
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log(opt, $"Error: {ex}");
            if (TrySteamFallbackAndHint(opt)) return 0;
            return 1;
        }
        finally
        {
            try { _logWriter?.Dispose(); } catch { }
        }
    }

    static Process? StartViaSteam(string url)
    {
        var psi = new ProcessStartInfo();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = url;
            psi.UseShellExecute = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "open";
            psi.Arguments = url;
            psi.UseShellExecute = false;
        }
        else // Linux
        {
            psi.FileName = "xdg-open";
            psi.Arguments = url;
            psi.UseShellExecute = false;
        }

        return Process.Start(psi);
    }

    static Process? StartViaExe(Options opt, string targetFull, string workDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = targetFull,
            Arguments = opt.Args ?? string.Empty,
            UseShellExecute = false,
            WorkingDirectory = workDir,
            CreateNoWindow = false
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = $"{workDir};{originalPath}";
            psi.Environment["DOORSTOP_ENABLE"] = "TRUE";
            psi.Environment["DOORSTOP_TARGET_ASSEMBLY"] = Path.Combine(workDir, "BepInEx", "core", "BepInEx.Preloader.dll");
        }
        else
        {
            // Unix: LD_LIBRARY_PATH / DYLD_LIBRARY_PATH
            var libPathKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
            var originalLib = Environment.GetEnvironmentVariable(libPathKey) ?? "";
            psi.Environment[libPathKey] = $"{workDir}:{originalLib}";
            psi.Environment["DOORSTOP_ENABLE"] = "TRUE";
            psi.Environment["DOORSTOP_TARGET_ASSEMBLY"] = Path.Combine(workDir, "BepInEx", "core", "BepInEx.Preloader.dll");
        }

        return Process.Start(psi);
    }

    struct DoorstopStatus
    {
        public bool ProxyExists;
        public bool ConfigExists;
        public bool BepinDirExists;
    }

    static DoorstopStatus DoorstopCheck(string workDir)
    {
        bool proxyExists = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            proxyExists = File.Exists(Path.Combine(workDir, "winhttp.dll")) ||
                          File.Exists(Path.Combine(workDir, "version.dll"));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            proxyExists = File.Exists(Path.Combine(workDir, "libdoorstop.dylib"));
        }
        else // Linux
        {
            proxyExists = File.Exists(Path.Combine(workDir, "libdoorstop.so"));
        }

        var configExists = File.Exists(Path.Combine(workDir, "doorstop_config.ini"));
        var bepinDirExists = Directory.Exists(Path.Combine(workDir, "BepInEx"));

        return new DoorstopStatus
        {
            ProxyExists = proxyExists,
            ConfigExists = configExists,
            BepinDirExists = bepinDirExists
        };
    }

    static List<Process> GetChildProcessesWindows(int parentId)
    {
        var children = new List<Process>();

        // 只在 Windows 上使用 WMI
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return children;

        try
        {
            // 使用反射避免编译错误（如果没有 System.Management 包）
            var mgmtType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management");
            if (mgmtType == null) return children;

            var searcher = Activator.CreateInstance(mgmtType,
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");

            var getMethod = mgmtType.GetMethod("Get");
            var results = getMethod?.Invoke(searcher, null);

            if (results is System.Collections.IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    var pidProp = obj.GetType().GetProperty("ProcessId");
                    if (pidProp != null)
                    {
                        var childId = Convert.ToInt32(pidProp.GetValue(obj));
                        try { children.Add(Process.GetProcessById(childId)); } catch { }
                    }
                }
            }

            (searcher as IDisposable)?.Dispose();
        }
        catch { }

        return children;
    }

    static bool IsSteamProtocol(string s)
        => s.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);

    static string ResolveWorkDir(Options opt, string? targetFull)
    {
        if (!string.IsNullOrWhiteSpace(opt.WorkDir))
        {
            try { return Path.GetFullPath(opt.WorkDir!); } catch { }
            return opt.WorkDir!;
        }
        try
        {
            if (!string.IsNullOrEmpty(targetFull))
                return Path.GetDirectoryName(targetFull) ?? Environment.CurrentDirectory;
        }
        catch { }
        return Environment.CurrentDirectory;
    }

    static string? GetFullPathSafe(string path)
    {
        try
        {
            if (Path.IsPathFullyQualified(path)) return path;
            return Path.GetFullPath(path);
        }
        catch { return null; }
    }

    static void RunChild(Options opt, string file, string? arguments, int timeoutMs)
    {
        Log(opt, $"Running post: \"{file}\" {arguments}");
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log(opt, "[post][out] " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log(opt, "[post][err] " + e.Data); };

        if (!p.Start())
            throw new InvalidOperationException($"Failed to start: {file}");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : int.MaxValue))
        {
            try { p.Kill(true); } catch { }
            throw new TimeoutException($"Timeout after {timeoutMs} ms");
        }

        if (p.ExitCode != 0)
            throw new Exception($"Exit code: {p.ExitCode}");
    }

    static Options? Parse(string[] args)
    {
        var opt = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? next() => (i + 1 < args.Length) ? args[++i] : null;

            switch (a)
            {
                case "--target": opt.Target = next(); break;
                case "--args": opt.Args = next(); break;
                case "--waitPid": opt.WaitPid = int.TryParse(next(), out var pid) ? pid : null; break;
                case "--post": opt.Post = next(); break;
                case "--postArgs": opt.PostArgs = next(); break;
                case "--delayMs": if (int.TryParse(next(), out var d)) opt.DelayMs = d; break;
                case "--postTimeoutMs": if (int.TryParse(next(), out var t)) opt.PostTimeoutMs = t; break;
                case "--workdir": opt.WorkDir = next(); break;
                case "--log": opt.LogPath = next(); break;
                case "--verbose": opt.Verbose = true; break;
                case "--autoSteamFallback":
                    var v = next();
                    opt.AutoSteamFallback = v != null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
                    break;
                case "--steamAppId": opt.SteamAppId = next(); break;
                case "--childProcessCheckDelayMs":
                    if (int.TryParse(next(), out var cpd)) opt.ChildProcessCheckDelayMs = cpd;
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(opt.Target) ? null : opt;
    }

    static void Log(Options? opt, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}";
        Console.WriteLine(line);
        try { _logWriter?.WriteLine(line); } catch { }
    }

    static bool TrySteamFallbackAndHint(Options opt)
    {
        if (!opt.AutoSteamFallback)
        {
            Log(opt, "[Hint] 建议使用 steam://rungameid/<AppId>");
            return false;
        }

        if (string.IsNullOrWhiteSpace(opt.SteamAppId))
        {
            Log(opt, "[AutoFallback] 需要 --steamAppId");
            return false;
        }

        var url = $"steam://rungameid/{opt.SteamAppId}";
        Log(opt, $"[AutoFallback] {url}");

        try
        {
            var p = StartViaSteam(url);
            if (p == null)
            {
                Log(opt, "[AutoFallback] Failed");
                return false;
            }
            Log(opt, $"[AutoFallback] Started PID={p.Id}");
            return true;
        }
        catch (Exception e)
        {
            Log(opt, $"[AutoFallback] Error: {e.Message}");
            return false;
        }
    }
}
