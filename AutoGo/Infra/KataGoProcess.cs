using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoGo.Infra;

public class KataGoProcess : IDisposable
{
    private CancellationTokenSource? _cts;
    private StreamWriter? _inputWriter;
    private Process? _process;

    public void Dispose()
    {
        _cts?.Cancel();
        if (_process is { HasExited: false })
        {
            _process.Kill();
        }
        _process?.Dispose();
        _inputWriter?.Dispose();
        GC.SuppressFinalize(this);
    }

    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;

    public async Task<bool> StartAndWaitInit(string exePath, string configPath, string modelPath)
    {
        if (_process is { HasExited: false })
        {
            _process.Kill();
            _process?.Dispose();
        }

        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
        }
        _cts = new CancellationTokenSource();

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var argumentList = new List<string> { "analysis", "-config", configPath, "-model", modelPath };
        foreach (var arg in argumentList)
        {
            startInfo.ArgumentList.Add(arg);
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var startedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _process.Exited += (_, _) => startedSignal.TrySetResult(false);

        if (!_process.Start())
        {
            throw new Exception("Failed to start KataGo process.");
        }

        _inputWriter = _process.StandardInput;

        ErrorReceived += HandleStartedOutput;
        _ = Task.Run(() => ReadOutputLoop(_cts.Token), _cts.Token);
        _ = Task.Run(() => ReadErrorLoop(_cts.Token), _cts.Token);

        var started = await WaitForStartedLineAsync(startedSignal).ConfigureAwait(false);
        ErrorReceived -= HandleStartedOutput;
        return started;

        void HandleStartedOutput(string line)
        {
            if (line.Contains("Started, ready to begin handling requests", StringComparison.OrdinalIgnoreCase))
            {
                startedSignal.TrySetResult(true);
            }
        }
    }

    private async Task<bool> WaitForStartedLineAsync(TaskCompletionSource<bool> startedSignal)
    {
        return await startedSignal.Task.ConfigureAwait(false);
    }

    private async Task ReadOutputLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _process is { HasExited: false })
        {
            var line = await _process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false);
            if (line != null)
            {
                OutputReceived?.Invoke(line);
            }
        }
    }

    private async Task ReadErrorLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _process is { HasExited: false })
        {
            var error = await _process.StandardError.ReadLineAsync(token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(error))
            {
                ErrorReceived?.Invoke(error);
            }
        }
    }

    public async Task SendLine(string jsonQuery)
    {
        if (_inputWriter != null)
        {
            await _inputWriter.WriteLineAsync(jsonQuery).ConfigureAwait(false);
            await _inputWriter.FlushAsync().ConfigureAwait(false);
        }
    }
}
