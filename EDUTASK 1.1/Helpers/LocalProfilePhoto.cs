namespace EDUTASK_1._1.Helpers;

/// <summary>
/// Keeps a stale machine-local photo path away from native image handlers.
/// Profile photos are stored in the app data directory, so uninstalling the app
/// or opening the shared database on another machine can leave a valid-looking
/// absolute path whose file no longer exists.
/// </summary>
public static class LocalProfilePhoto
{
    public static string ExistingOrEmpty(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string candidate = path.Trim();
        try
        {
            return Path.IsPathFullyQualified(candidate) && !File.Exists(candidate)
                ? string.Empty
                : candidate;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
