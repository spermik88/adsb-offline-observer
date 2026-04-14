using System.Globalization;
using System.Diagnostics;
using System.Net.Sockets;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class DecoderProcessService : IDecoderProcessService
{
    private readonly PortableWorkspacePaths? _workspace;
    private Process? _process;
    private string? _lastOutputLine;
    private string? _lastErrorLine;

    public DecoderProcessService(PortableWorkspacePaths? workspace = null)
    {
        _workspace = workspace;
    }

    public event EventHandler<DecoderProcessStatus>? StatusChanged;

    public bool IsRunning => _process is { HasExited: false };

    public DecoderProcessStatus CurrentStatus { get; private set; } =
        new(DecoderProcessState.Stopped, "dump1090: stopped");

    public async Task<DecoderProcessStatus> StartAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.DecoderAutoStart)
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Disabled,
                "dump1090: auto-start disabled",
                FailureReason: DecoderFailureReason.AutoStartDisabled));
        }

        var executablePath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                $"dump1090: bundled backend missing at {executablePath ?? "<not resolved>"}",
                FailureReason: DecoderFailureReason.BackendMissing,
                ExecutablePath: executablePath,
                LastErrorLine: "Bundled dump1090 backend was not found."));
        }

        if (await IsPortReachableAsync(settings.DecoderHost, settings.DecoderPort, cancellationToken))
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Ready,
                $"dump1090: SBS-1 port {settings.DecoderHost}:{settings.DecoderPort} already reachable",
                IsReady: true,
                PortReachable: true,
                ExecutablePath: executablePath));
        }

        if (IsRunning)
        {
            return CurrentStatus;
        }

        var configPath = PrepareRuntimeConfig(settings, executablePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = Dump1090ArgumentBuilder.Build(settings, configPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += OnProcessExited;

        UpdateStatus(new DecoderProcessStatus(
            DecoderProcessState.Starting,
            $"dump1090: starting bundled backend on {settings.DecoderHost}:{settings.DecoderPort}",
            ExecutablePath: executablePath));

        if (!_process.Start())
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                "dump1090: failed to start bundled backend",
                FailureReason: DecoderFailureReason.StartFailed,
                ExecutablePath: executablePath));
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var ready = await WaitForPortAsync(settings.DecoderHost, settings.DecoderPort, TimeSpan.FromSeconds(15), cancellationToken);
        return ready
            ? UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Ready,
                $"dump1090: ready on {settings.DecoderHost}:{settings.DecoderPort}",
                IsReady: true,
                PortReachable: true,
                ExecutablePath: executablePath,
                LastOutputLine: _lastOutputLine,
                LastErrorLine: _lastErrorLine))
            : UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                _process is { HasExited: true }
                    ? "dump1090: backend exited before SBS-1 port became ready"
                    : $"dump1090: started but port {settings.DecoderHost}:{settings.DecoderPort} is not reachable",
                FailureReason: _process is { HasExited: true }
                    ? DecoderFailureReason.ProcessExitedEarly
                    : DecoderFailureReason.PortUnavailable,
                ExecutablePath: executablePath,
                LastOutputLine: _lastOutputLine,
                LastErrorLine: _lastErrorLine ?? "dump1090 did not expose the SBS-1 port in time."));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "dump1090: stopped"));
            return;
        }

        if (_process.HasExited)
        {
            CleanupProcess();
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "dump1090: stopped"));
            return;
        }

        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            CleanupProcess();
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "dump1090: stopped"));
        }
    }

    private string PrepareRuntimeConfig(ObservationSettings settings, string executablePath)
    {
        var templatePath = BundledAssetPathResolver.ResolveDecoderConfig(settings);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("Bundled dump1090 config was not found.", templatePath);
        }

        var logPath = _workspace is null
            ? BundledAssetPathResolver.ResolveDecoderLog(settings)
            : Path.Combine(_workspace.LogsRoot, Path.GetFileName(settings.BundledDecoderLogRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? AppContext.BaseDirectory);

        var runtimeConfigPath = _workspace is null
            ? Path.Combine(Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory, "dump1090.runtime.cfg")
            : Path.Combine(_workspace.LogsRoot, "dump1090.runtime.cfg");

        var configLines = File.ReadAllLines(templatePath).ToList();
        RemoveConfigKey(configLines, "net-bo-port");
        UpsertConfigValue(configLines, "logfile", logPath);
        UpsertConfigValue(configLines, "gain", settings.Gain.ToString(CultureInfo.InvariantCulture));
        UpsertConfigValue(configLines, "samplerate", settings.SampleRate.ToString(CultureInfo.InvariantCulture));
        UpsertConfigValue(configLines, "rtlsdr-ppm", settings.PpmCorrection.ToString(CultureInfo.InvariantCulture));
        UpsertConfigValue(configLines, "net-sbs-port", settings.DecoderPort.ToString(CultureInfo.InvariantCulture));
        UpsertConfigValue(configLines, "homepos", $"{settings.CenterLatitude.ToString(CultureInfo.InvariantCulture)},{settings.CenterLongitude.ToString(CultureInfo.InvariantCulture)}");
        UpsertConfigValue(configLines, "aircrafts", "NUL");
        UpsertConfigValue(configLines, "airports", "NUL");
        RemoveConfigKey(configLines, "location");
        UpsertConfigValue(configLines, "web-page", "NUL");

        File.WriteAllLines(runtimeConfigPath, configLines);
        return runtimeConfigPath;
    }

    private static void UpsertConfigValue(IList<string> lines, string key, string value)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith($"{key} =", StringComparison.OrdinalIgnoreCase))
            {
                lines[index] = $"{key} = {value}";
                return;
            }
        }

        lines.Add($"{key} = {value}");
    }

    private static void RemoveConfigKey(IList<string> lines, string key)
    {
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith($"{key} =", StringComparison.OrdinalIgnoreCase))
            {
                lines.RemoveAt(index);
            }
        }
    }

    private static async Task<bool> WaitForPortAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsPortReachableAsync(host, port, cancellationToken))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    private static async Task<bool> IsPortReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private void CleanupProcess()
    {
        if (_process is null)
        {
            return;
        }

        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Exited -= OnProcessExited;
        _process.Dispose();
        _process = null;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        _lastOutputLine = args.Data;
        UpdateStatus(CurrentStatus with { LastOutputLine = _lastOutputLine, Message = "dump1090: running" });
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        _lastErrorLine = args.Data;
        UpdateStatus(CurrentStatus with { LastErrorLine = _lastErrorLine, Message = "dump1090: emitted errors" });
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        UpdateStatus(CurrentStatus with { State = DecoderProcessState.Stopped, IsReady = false, Message = "dump1090 exited" });
    }

    private DecoderProcessStatus UpdateStatus(DecoderProcessStatus status)
    {
        CurrentStatus = status;
        StatusChanged?.Invoke(this, status);
        return status;
    }
}
