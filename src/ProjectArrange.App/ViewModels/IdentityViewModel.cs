using System.IO;
using ProjectArrange.App.Mvvm;
using ProjectArrange.Infrastructure.Configuration;
using ProjectArrange.Infrastructure.P2p;
using ProjectArrange.Infrastructure.Presence;

namespace ProjectArrange.App.ViewModels;

public sealed class IdentityViewModel : ObservableObject
{
    private readonly MachineIdentityProvider _machine;
    private readonly P2pIdentityProvider _p2p;

    private string _text = "";

    public IdentityViewModel(MachineIdentityProvider machine, P2pIdentityProvider p2p)
    {
        _machine = machine;
        _p2p = p2p;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public AsyncCommand RefreshCommand { get; }

    private Task RefreshAsync()
    {
        var deviceId = _p2p.GetOrCreateDeviceId();
        var cert = _p2p.GetOrCreateCertificate();

        var machineIdPath = Path.Combine(AppPaths.GetAppDataRoot(), "machine_id.txt");
        var p2pIdPath = Path.Combine(AppPaths.GetAppDataRoot(), "p2p_device_id.txt");
        var p2pCertPath = Path.Combine(AppPaths.GetAppDataRoot(), "p2p_cert.pfx");

        Text =
            $"MachineName={_machine.GetMachineName()}{Environment.NewLine}" +
            $"MachineId={_machine.GetMachineId()}{Environment.NewLine}" +
            $"MachineIdPath={machineIdPath}{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"P2P DeviceName={_p2p.GetDeviceName()}{Environment.NewLine}" +
            $"P2P DeviceId={deviceId}{Environment.NewLine}" +
            $"P2P CertThumbprint={cert.GetCertHashString()}{Environment.NewLine}" +
            $"P2P DeviceIdPath={p2pIdPath}{Environment.NewLine}" +
            $"P2P CertPath={p2pCertPath}";

        return Task.CompletedTask;
    }
}
