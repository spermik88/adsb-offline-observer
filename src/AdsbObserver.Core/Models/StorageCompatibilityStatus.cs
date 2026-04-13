namespace AdsbObserver.Core.Models;

public sealed record StorageCompatibilityStatus(
    bool IsCompatible,
    bool RequiresMigration,
    int CurrentVersion,
    int DetectedVersion,
    string Message);
