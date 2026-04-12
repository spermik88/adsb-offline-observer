using System.IO;
using System.Windows;
using AdsbObserver.App.ViewModels;
using AdsbObserver.Core.Interfaces;
using AdsbObserver.Core.Services;
using AdsbObserver.Infrastructure.Repositories;
using AdsbObserver.Infrastructure.Services;

namespace AdsbObserver.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AdsbObserver");
        Directory.CreateDirectory(dataRoot);

        IStorageService storageService = new SqliteStorageService(Path.Combine(dataRoot, "observer.db"));
        await storageService.InitializeAsync(CancellationToken.None);

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
            dataRoot);

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
