using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class DatabaseViewModel : ObservableObject
{
    private readonly IDatabaseMigrator _migrator;
    private string _summary = "";
    private string _details = "";

    public DatabaseViewModel(IDatabaseMigrator migrator)
    {
        _migrator = migrator;
        MigrateCommand = new AsyncCommand(MigrateAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public AsyncCommand MigrateCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    private async Task RefreshAsync()
    {
        var v = await _migrator.GetSchemaVersionAsync();
        if (!v.IsSuccess)
        {
            Summary = "DB error";
            Details = v.Error ?? "";
            return;
        }

        Summary = $"SchemaVersion: {v.Value}";
        Details = "";
    }

    private async Task MigrateAsync()
    {
        Summary = "Migrating...";
        Details = "";

        var r = await _migrator.MigrateAsync();
        if (!r.IsSuccess)
        {
            Summary = "Migration failed";
            Details = r.Error ?? "";
            return;
        }

        await RefreshAsync();
    }
}
