using ProjectArrange.App.Mvvm;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.App.ViewModels;

public sealed class ProcessViewModel : ObservableObject
{
    private readonly IProcessRunner _runner;

    private string _fileName = "git";
    private string _arguments = "--version";
    private string _workingDirectory = Environment.CurrentDirectory;
    private int _timeoutMs = 10000;
    private string _result = "";

    public ProcessViewModel(IProcessRunner runner)
    {
        _runner = runner;
        RunCommand = new AsyncCommand(RunAsync);
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public int TimeoutMs
    {
        get => _timeoutMs;
        set => SetProperty(ref _timeoutMs, value);
    }

    public string Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public AsyncCommand RunCommand { get; }

    private async Task RunAsync()
    {
        var spec = new ProcessSpec(
            FileName: FileName,
            Arguments: Arguments,
            WorkingDirectory: string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
            TimeoutMilliseconds: TimeoutMs <= 0 ? 10000 : TimeoutMs);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = await _runner.RunAsync(spec);
        sw.Stop();

        Result =
            $"ExitCode={res.ExitCode} TimedOut={res.TimedOut} ElapsedMs={sw.ElapsedMilliseconds}{Environment.NewLine}" +
            $"--- STDOUT ---{Environment.NewLine}{res.StdOut}{Environment.NewLine}" +
            $"--- STDERR ---{Environment.NewLine}{res.StdErr}";
    }
}

