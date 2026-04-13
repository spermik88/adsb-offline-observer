using System.IO;
using System.Windows;
using AdsbObserver.App.ViewModels;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Models;
using AdsbObserver.Infrastructure.Repositories;
using AdsbObserver.Infrastructure.Services;
using AdsbObserver.Core.Services;

namespace AdsbObserver.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var workspace = PortableWorkspacePathResolver.Resolve(AppContext.BaseDirectory);

        IStorageService storageService = new SqliteStorageService(Path.Combine(workspace.DataRoot, "observer.db"));
        var compatibility = await storageService.InitializeAsync(CancellationToken.None);
        if (!compatibility.IsCompatible)
        {
            MessageBox.Show(
                compatibility.Message,
                "Portable data incompatible",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var viewModel = new MainViewModel(
            storageService,
            new RtlSdrDeviceDetector(),
            new Sbs1TcpDecoderAdapter(),
            new SimulatedDecoderAdapter(),
            new DecoderProcessService(),
            new SdrDriverBootstrapService(new RtlSdrDeviceDetector()),
            new CsvRecognitionImportService(storageService),
            new CsvTrackExportService(storageService),
            new MbTilesMapService(),
            new AircraftTrackerService(),
            new PlaybackService(),
            workspace,
            compatibility);

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
