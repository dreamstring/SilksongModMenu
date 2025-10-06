using System.Diagnostics;

namespace RestartStub;

internal class Options
{
    public string? Target;       // 目标：game.exe 或 steam://rungameid/xxxxx
    public string? Args;         // 启动参数
    public int? WaitPid;         // 等待此 PID 退出
    public string? Post;         // 后处理器可执行文件
    public string? PostArgs;     // 后处理器参数
    public int DelayMs = 1200;   // 缓冲等待
    public int PostTimeoutMs = 30000; // 后处理器最长等待时间
    public bool Verbose = false; // 详细日志
    public string? WorkDir;      // 工作目录（exe 启动时生效）
    public string? LogPath;      // 日志输出路径

    // 新增：当直接 exe 启动可能不注入时，自动回退到 steam://
    public bool AutoSteamFallback = false;
    public string? SteamAppId;   // 与 AutoSteamFallback 搭配使用，或直接用 --target steam://rungameid/xxx

    // 新增：子进程检测等待时间（毫秒）
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
            Console.WriteLine("Usage: RestartStub --target \"game.exe|steam://rungameid/XXXXXX\" [--args \"...\"] [--waitPid 1234] [--post \"ModPostProcessor.exe\" --postArgs \"...\"] [--delayMs 1200] [--postTimeoutMs 30000] [--workdir \".\"] [--log \"stub.log\"] [--verbose] [--autoSteamFallback true|false] [--steamAppId 123456]");
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

            if (!isSteamProtocol)
            {
                if (targetFull is null || !File.Exists(targetFull))
                {
                    Log(opt, $"Error: target not found -> '{opt.Target}' (full='{targetFull ?? "null"}')");
                    TrySteamFallbackAndHint(opt);
                    return 2;
                }
            }

            Log(opt, $"[RestartStub] start. target='{opt.Target}' (full='{targetFull ?? "steam://"}'), waitPid={opt.WaitPid}, workdir='{(isSteamProtocol ? "(steam)" : workDir)}', autoSteamFallback={opt.AutoSteamFallback}");

            // 1) 等待指定 PID 退出（如果提供）
            if (opt.WaitPid is int pid && pid > 0)
            {
                try
                {
                    var p = Process.GetProcessById(pid);
                    Log(opt, $"Waiting PID {pid} to exit...");
                    try { p.WaitForExit(); } catch { /* ignore */ }
                }
                catch
                {
                    Log(opt, $"PID {pid} not found (already exited).");
                }
            }

            // 2) 缓冲等待
            if (opt.DelayMs > 0)
            {
                Log(opt, $"Delay {opt.DelayMs} ms...");
                Thread.Sleep(opt.DelayMs);
            }

            // 3) 执行后处理器（可选）
            if (!string.IsNullOrWhiteSpace(opt.Post))
            {
                RunChild(opt, opt.Post!, opt.PostArgs, opt.PostTimeoutMs);
            }

            // 3.5) 若非 steam://，启动前切到工作目录（双保险）+ Doorstop 自检
            if (!isSteamProtocol)
            {
                try
                {
                    Directory.SetCurrentDirectory(workDir);
                    Log(opt, $"[Info] Current directory set to: {Environment.CurrentDirectory}");
                }
                catch (Exception e)
                {
                    Log(opt, $"Warn: SetCurrentDirectory failed: {e.Message}");
                }

                var check = DoorstopCheck(workDir);
                Log(opt, $"[Check] winhttp.dll: {check.WinhttpExists}; version.dll: {check.VersionExists}; doorstop_config.ini: {check.DoorstopIniExists}; BepInEx/: {check.BepinDirExists}");

                if (!check.BepinDirExists || !check.DoorstopIniExists || (!check.WinhttpExists && !check.VersionExists))
                {
                    Log(opt, "[Hint] Doorstop/BepInEx 文件不完整，直接启动可能不会注入。");
                    if (TrySteamFallbackAndHint(opt))
                    {
                        return 0;
                    }
                }
                else
                {
                    Log(opt, "[Info] Doorstop 文件齐全，但部分 Unity/Steam 启动路径不会早期加载这些 DLL；若仍不注入，建议使用 steam:// 启动。");
                }
            }

            // 4) 启动目标进程
            Process? proc;

            if (isSteamProtocol)
            {
                // Steam 协议启动
                var psi = new ProcessStartInfo
                {
                    FileName = opt.Target!,
                    Arguments = string.Empty,
                    UseShellExecute = true,  // steam:// 必须用 Shell 托管
                    WorkingDirectory = ""
                };

                Log(opt, $"Starting via Steam protocol: \"{psi.FileName}\"");
                proc = Process.Start(psi);
            }
            else
            {
                // 直接 exe 启动 - 强化版
                var originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                var enhancedPath = $"{workDir};{originalPath}";

                var psi = new ProcessStartInfo
                {
                    FileName = targetFull!,
                    Arguments = opt.Args ?? string.Empty,
                    UseShellExecute = false,  // 必须 false 才能修改环境变量
                    WorkingDirectory = workDir,
                    CreateNoWindow = false
                };

                // 强制 DLL 搜索优先当前目录
                psi.Environment["PATH"] = enhancedPath;

                // 添加 Doorstop 可能需要的环境变量（可选，某些版本需要）
                psi.Environment["DOORSTOP_ENABLE"] = "TRUE";
                psi.Environment["DOORSTOP_TARGET_ASSEMBLY"] = Path.Combine(workDir, "BepInEx", "core", "BepInEx.Preloader.dll");

                Log(opt, $"Starting target: \"{psi.FileName}\" {psi.Arguments}");
                Log(opt, $"WD: '{psi.WorkingDirectory}', exists={Directory.Exists(psi.WorkingDirectory)}");
                Log(opt, $"Enhanced PATH: {enhancedPath.Substring(0, Math.Min(200, enhancedPath.Length))}...");

                proc = Process.Start(psi);
            }

            if (proc is null)
            {
                Log(opt, "Failed to start target process.");
                if (TrySteamFallbackAndHint(opt))
                    return 0;
                return 3;
            }

            Log(opt, $"Target started. PID={proc.Id}");

            // 5) 子进程检测（仅非 Steam 协议时）
            if (!isSteamProtocol && opt.ChildProcessCheckDelayMs > 0)
            {
                Thread.Sleep(opt.ChildProcessCheckDelayMs);

                if (!proc.HasExited)
                {
                    var children = GetChildProcesses(proc.Id);
                    if (children.Count > 0)
                    {
                        Log(opt, $"[Warning] Detected {children.Count} child process(es). Game may use launcher->child model:");
                        foreach (var child in children)
                        {
                            try
                            {
                                Log(opt, $"  - Child PID={child.Id}, Name={child.ProcessName}, Path={child.MainModule?.FileName ?? "N/A"}");
                            }
                            catch
                            {
                                Log(opt, $"  - Child PID={child.Id} (details unavailable)");
                            }
                        }
                        Log(opt, "[Hint] Doorstop may only inject into launcher process. Consider using steam:// protocol.");

                        // 如果启用了自动回退，且检测到子进程，可以选择杀掉当前进程并回退
                        if (opt.AutoSteamFallback && !string.IsNullOrWhiteSpace(opt.SteamAppId))
                        {
                            Log(opt, "[AutoFallback] Killing launcher and restarting via Steam...");
                            try
                            {
                                proc.Kill(true);
                                foreach (var child in children)
                                {
                                    try { child.Kill(true); } catch { }
                                }
                            }
                            catch { }

                            Thread.Sleep(1000);
                            return TrySteamFallbackAndHint(opt) ? 0 : 3;
                        }
                    }
                    else
                    {
                        Log(opt, "[Info] No child processes detected. Game appears to run in single-process mode.");
                    }
                }
                else
                {
                    Log(opt, "[Warning] Target process exited immediately. This may indicate a launcher or error.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log(opt, $"Error: {ex}");
            if (TrySteamFallbackAndHint(opt))
                return 0;
            return 1;
        }
        finally
        {
            try { _logWriter?.Dispose(); } catch { /* ignore */ }
        }
    }

    static bool IsSteamProtocol(string s)
        => s.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);

    static string ResolveWorkDir(Options opt, string? targetFull)
    {
        if (!string.IsNullOrWhiteSpace(opt.WorkDir))
        {
            try { return Path.GetFullPath(opt.WorkDir!); } catch { /* ignore */ }
            return opt.WorkDir!;
        }
        try
        {
            if (!string.IsNullOrEmpty(targetFull))
                return Path.GetDirectoryName(targetFull) ?? Environment.CurrentDirectory;
        }
        catch { /* ignore */ }
        return Environment.CurrentDirectory;
    }

    struct DoorstopStatus
    {
        public bool WinhttpExists;
        public bool VersionExists;
        public bool DoorstopIniExists;
        public bool BepinDirExists;
    }

    static DoorstopStatus DoorstopCheck(string workDir)
    {
        var winhttp = Path.Combine(workDir, "winhttp.dll");
        var version = Path.Combine(workDir, "version.dll");
        var doorstopIni = Path.Combine(workDir, "doorstop_config.ini");
        var bepinDir = Path.Combine(workDir, "BepInEx");

        return new DoorstopStatus
        {
            WinhttpExists = File.Exists(winhttp),
            VersionExists = File.Exists(version),
            DoorstopIniExists = File.Exists(doorstopIni),
            BepinDirExists = Directory.Exists(bepinDir)
        };
    }

    static List<Process> GetChildProcesses(int parentId)
    {
        var children = new List<Process>();
        try
        {
            // 使用 WMI 查询子进程（需要 System.Management 包）
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={parentId}");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var childId = Convert.ToInt32(obj["ProcessId"]);
                try
                {
                    children.Add(Process.GetProcessById(childId));
                }
                catch
                {
                    // 进程可能已退出
                }
            }
        }
        catch (Exception ex)
        {
            // WMI 查询失败（可能权限不足或平台不支持）
            // 静默失败，不影响主流程
            Console.WriteLine($"[Debug] WMI query failed: {ex.Message}");
        }
        return children;
    }

    static string? GetFullPathSafe(string path)
    {
        try
        {
            if (Path.IsPathFullyQualified(path))
                return path;
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
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
            throw new InvalidOperationException($"Failed to start post process: {file}");

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : int.MaxValue))
        {
            try { p.Kill(true); } catch { /* ignore */ }
            throw new TimeoutException($"Post process timeout after {timeoutMs} ms: {file}");
        }

        Log(opt, $"Post exit code: {p.ExitCode}");
        if (p.ExitCode != 0)
            throw new Exception($"Post process returned {p.ExitCode}");
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
                    {
                        var v = next();
                        opt.AutoSteamFallback = v != null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
                        break;
                    }
                case "--steamAppId":
                    opt.SteamAppId = next();
                    break;
                case "--childProcessCheckDelayMs":
                    if (int.TryParse(next(), out var cpd)) opt.ChildProcessCheckDelayMs = cpd;
                    break;
                default:
                    // 忽略未知参数
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(opt.Target))
            return null;

        return opt;
    }

    static void Log(Options? opt, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}";
        Console.WriteLine(line);
        try { _logWriter?.WriteLine(line); } catch { /* ignore */ }
    }

    static bool TrySteamFallbackAndHint(Options opt)
    {
        if (!opt.AutoSteamFallback)
        {
            Log(opt, "[Hint] 若重启后 BepInEx 不注入，请改用 --target \"steam://rungameid/<AppId>\"，或启动时加 --autoSteamFallback true 并提供 --steamAppId <AppId>。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(opt.SteamAppId))
        {
            Log(opt, "[AutoSteamFallback] 需要 --steamAppId <AppId> 才能回退到 steam:// 启动。");
            return false;
        }

        var url = $"steam://rungameid/{opt.SteamAppId}";
        Log(opt, $"[AutoSteamFallback] Fallback to Steam: {url}");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true, // 必须
                Arguments = string.Empty
            };
            var p = Process.Start(psi);
            if (p == null)
            {
                Log(opt, "[AutoSteamFallback] Failed to start steam:// process.");
                return false;
            }
            Log(opt, $"[AutoSteamFallback] Started via Steam. PID={p.Id}");
            return true;
        }
        catch (Exception e)
        {
            Log(opt, $"[AutoSteamFallback] Error: {e.Message}");
            return false;
        }
    }
}
