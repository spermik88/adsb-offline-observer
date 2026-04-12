namespace AdsbObserver.Core.Models;

public sealed record MapTileData(int Zoom, int X, int Y, byte[] Bytes);
