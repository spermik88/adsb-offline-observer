using System.Diagnostics;
using System.Net.Sockets;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;

namespace AdsbObserver.Infrastructure.Services;

public sealed class DecoderProcessService : IDecoderProcessService
{
    private Process? _process;

    public event EventHandler<string>? StatusChanged;

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(ObservationSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.DecoderAutoStart)
        {
            OnStatusChanged("Decoder process: auto-start disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DecoderExecutablePath))
        {
            OnStatusChanged("Decoder process: executable path is not configured");
            return;
        }

        if (IsRunning)
        {
            OnStatusChanged("Decoder process: already running");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = settings.DecoderExecutablePath,
            Arguments = settings.DecoderArguments ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        _process.Exited += OnProcessExited;

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start decoder process");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        OnStatusChanged($"Decoder process started: {Path.GetFileName(settings.DecoderExecutablePath)}");

        var ready = await WaitForPortAsync(settings.DecoderHost, settings.DecoderPort, TimeSpan.FromSeconds(12), cancellationToken);
        OnStatusChanged(
            ready
                ? $"Decoder process ready on {settings.DecoderHost}:{settings.DecoderPort}"
                : $"Decoder process started, but port {settings.DecoderHost}:{settings.DecoderPort} is not reachable");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            return;
        }

        if (_process.HasExited)
        {
            CleanupProcess();
            OnStatusChanged("Decoder process: stopped");
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
            OnStatusChanged("Decoder process: stopped");
        }
    }

    private static async Task<bool> WaitForPortAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, cancellationToken);
                return client.Connected;
            }
            catch
            {
                await Task.Delay(300, cancellationToken);
            }
        }

        return false;
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
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            OnStatusChanged($"Decoder stdout: {args.Data}");
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            OnStatusChanged($"Decoder stderr: {args.Data}");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        OnStatusChanged("Decoder process exited");
    }

    private void OnStatusChanged(string message)
    {
        StatusChanged?.Invoke(this, message);
    }
}
