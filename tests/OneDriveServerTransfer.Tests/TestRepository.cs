namespace OneDriveServerTransfer.Tests;

/// <summary>
/// Locates the repository root from the test assembly output directory so tests can
/// inspect committed solution files.
/// </summary>
internal static class TestRepository
{
    public static string Root { get; } = LocateRoot();

    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OneDriveServerTransfer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root containing OneDriveServerTransfer.sln.");
    }
}
