using System.Diagnostics;
using System.Net.Sockets;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class DecoderProcessService : IDecoderProcessService
{
    private Process? _process;
    private string? _lastOutputLine;
    private string? _lastErrorLine;

    public event EventHandler<DecoderProcessStatus>? StatusChanged;

    public bool IsRunning => _process is { HasExited: false };

    public DecoderProcessStatus CurrentStatus { get; private set; } =
        new(DecoderProcessState.Stopped, "Decoder process: stopped");

    public async Task<DecoderProcessStatus> StartAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.DecoderAutoStart)
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Disabled,
                "Decoder process: auto-start disabled",
                FailureReason: DecoderFailureReason.AutoStartDisabled));
        }

        var executablePath = BundledAssetPathResolver.ResolveDecoderExecutable(settings);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                $"Decoder process: bundled backend missing at {executablePath ?? "<not resolved>"}",
                FailureReason: DecoderFailureReason.BackendMissing,
                ExecutablePath: executablePath,
                LastErrorLine: "Bundled readsb backend was not found."));
        }

        if (await IsPortReachableAsync(settings.DecoderHost, settings.DecoderPort, cancellationToken))
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Ready,
                $"Decoder process: SBS-1 port {settings.DecoderHost}:{settings.DecoderPort} already reachable",
                IsReady: true,
                PortReachable: true,
                ExecutablePath: executablePath));
        }

        if (IsRunning)
        {
            return CurrentStatus;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = ReadsbArgumentBuilder.Build(settings),
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
            $"Decoder process: starting bundled readsb on {settings.DecoderHost}:{settings.DecoderPort}",
            ExecutablePath: executablePath));

        if (!_process.Start())
        {
            return UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                "Decoder process: failed to start bundled backend",
                FailureReason: DecoderFailureReason.StartFailed,
                ExecutablePath: executablePath));
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var ready = await WaitForPortAsync(settings.DecoderHost, settings.DecoderPort, TimeSpan.FromSeconds(15), cancellationToken);
        return ready
            ? UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Ready,
                $"Decoder process: ready on {settings.DecoderHost}:{settings.DecoderPort}",
                IsReady: true,
                PortReachable: true,
                ExecutablePath: executablePath,
                LastOutputLine: _lastOutputLine,
                LastErrorLine: _lastErrorLine))
            : UpdateStatus(new DecoderProcessStatus(
                DecoderProcessState.Failed,
                _process is { HasExited: true }
                    ? "Decoder process: backend exited before SBS-1 port became ready"
                    : $"Decoder process: started but port {settings.DecoderHost}:{settings.DecoderPort} is not reachable",
                FailureReason: _process is { HasExited: true }
                    ? DecoderFailureReason.ProcessExitedEarly
                    : DecoderFailureReason.PortUnavailable,
                ExecutablePath: executablePath,
                LastOutputLine: _lastOutputLine,
                LastErrorLine: _lastErrorLine ?? "Backend did not expose the SBS-1 port in time."));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "Decoder process: stopped"));
            return;
        }

        if (_process.HasExited)
        {
            CleanupProcess();
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "Decoder process: stopped"));
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
            UpdateStatus(new DecoderProcessStatus(DecoderProcessState.Stopped, "Decoder process: stopped"));
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
        UpdateStatus(CurrentStatus with { LastOutputLine = _lastOutputLine, Message = "Decoder process: running" });
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        _lastErrorLine = args.Data;
        UpdateStatus(CurrentStatus with { LastErrorLine = _lastErrorLine, Message = "Decoder process: emitted errors" });
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        UpdateStatus(CurrentStatus with { State = DecoderProcessState.Stopped, IsReady = false, Message = "Decoder process exited" });
    }

    private DecoderProcessStatus UpdateStatus(DecoderProcessStatus status)
    {
        CurrentStatus = status;
        StatusChanged?.Invoke(this, status);
        return status;
    }
}
