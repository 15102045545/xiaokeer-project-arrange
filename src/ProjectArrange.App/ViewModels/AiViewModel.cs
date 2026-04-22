using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Ai;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.App.ViewModels;

public sealed class AiViewModel : ObservableObject
{
    private readonly IAiService _ai;
    private readonly IConfiguration _configuration;
    private readonly IToolLocator _tools;

    private string _prompt = "Hello";
    private string _system = "";
    private string _response = "";
    private string _status = "";
    private Process? _pythonProcess;

    public AiViewModel(IAiService ai, IConfiguration configuration, IToolLocator tools)
    {
        _ai = ai;
        _configuration = configuration;
        _tools = tools;

        SendCommand = new AsyncCommand(SendAsync);
        StartPythonServerCommand = new AsyncCommand(StartPythonServerAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);

        _ = RefreshAsync();
    }

    public string Prompt
    {
        get => _prompt;
        set => SetProperty(ref _prompt, value);
    }

    public string System
    {
        get => _system;
        set => SetProperty(ref _system, value);
    }

    public string Response
    {
        get => _response;
        set => SetProperty(ref _response, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public AsyncCommand SendCommand { get; }
    public AsyncCommand StartPythonServerCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    private Task RefreshAsync()
    {
        Status = $"Ai:Endpoint={_configuration["Ai:Endpoint"]}";
        return Task.CompletedTask;
    }

    private async Task SendAsync()
    {
        Status = "Sending...";
        var res = await _ai.ChatAsync(new AiChatRequest(Prompt, string.IsNullOrWhiteSpace(System) ? null : System));
        if (!res.IsSuccess)
        {
            Status = $"Fail: {res.Error}";
            return;
        }

        Response = res.Value?.Text ?? "";
        Status = "OK";
    }

    private Task StartPythonServerAsync()
    {
        try
        {
            if (_pythonProcess is { HasExited: false })
            {
                Status = "Python AI server already running";
                return Task.CompletedTask;
            }

            var endpointRaw = _configuration["Ai:Endpoint"] ?? "http://127.0.0.1:7312";
            if (!Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint))
            {
                Status = "Invalid Ai:Endpoint";
                return Task.CompletedTask;
            }

            var host = endpoint.Host;
            var port = endpoint.Port;

            var python = _tools.Resolve("python");
            var script = Path.Combine(AppContext.BaseDirectory, "python", "ai_server.py");
            if (!File.Exists(script))
            {
                Status = $"Missing script: {script}";
                return Task.CompletedTask;
            }

            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"-u \"{script}\" --host {host} --port {port}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _pythonProcess = Process.Start(psi);
            Status = _pythonProcess is null ? "Start failed" : $"Started: {python} ({host}:{port})";
        }
        catch (Exception ex)
        {
            Status = $"Start failed: {ex.Message}";
        }

        return Task.CompletedTask;
    }
}
