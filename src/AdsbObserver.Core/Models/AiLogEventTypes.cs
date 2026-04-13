namespace AdsbObserver.Core.Models;

public static class AiLogEventTypes
{
    public const string AppSession = "app.session";
    public const string UiCommand = "ui.command";
    public const string UiStateChange = "ui.state_change";
    public const string FiltersChanged = "filters.changed";
    public const string SelectionChanged = "selection.changed";
    public const string MapRender = "map.render";
    public const string MapTiles = "map.tiles";
    public const string Playback = "playback";
    public const string LiveDecoder = "live.decoder";
    public const string LiveEnvironment = "live.environment";
    public const string Storage = "storage";
    public const string Export = "export";
    public const string Error = "error";
    public const string Exception = "exception";
}
