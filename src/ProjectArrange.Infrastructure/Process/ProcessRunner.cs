using System.Diagnostics;

namespace ProjectArrange.Infrastructure.Process;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        using var process = new System.Diagnostics.Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = spec.StdInText is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (spec.Environment is not null)
        {
            foreach (var kv in spec.Environment)
            {
                process.StartInfo.Environment[kv.Key] = kv.Value;
            }
        }

        try
        {
            if (!process.Start())
            {
                return new ProcessResult(-1, string.Empty, "Failed to start process.", false);
            }
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.ToString(), false);
        }

        using var timeoutCts = new CancellationTokenSource(spec.TimeoutMilliseconds);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            Task writeTask = Task.CompletedTask;
            if (spec.StdInText is not null)
            {
                writeTask = Task.Run(async () =>
                {
                    await process.StandardInput.WriteAsync(spec.StdInText.AsMemory(), linkedCts.Token);
                    process.StandardInput.Close();
                }, linkedCts.Token);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await Task.WhenAll(writeTask, stdoutTask, stderrTask, process.WaitForExitAsync(linkedCts.Token));
            return new ProcessResult(
                process.ExitCode,
                stdoutTask.Result,
                stderrTask.Result,
                false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch
            {
            }

            return new ProcessResult(
                -1,
                string.Empty,
                string.Empty,
                true);
        }
    }
}
