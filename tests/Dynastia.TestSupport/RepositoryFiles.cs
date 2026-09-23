namespace Dynastia.TestSupport;

/// <summary>Source-artifact access shared by tests; independent of the working directory.</summary>
public static class RepositoryFiles
{
    private static readonly Lazy<string> CachedRoot = new(LocateRoot);

    public static string Root => CachedRoot.Value;

    public static string Path(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return System.IO.Path.Combine(new[] { Root }.Concat(parts).ToArray());
    }

    public static string ReadText(params string[] parts) => File.ReadAllText(Path(parts));

    public static string[] ReadLines(params string[] parts) => File.ReadAllLines(Path(parts));

    private static string LocateRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Dynastia.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Dynastia.slnx above test output '{AppContext.BaseDirectory}'. " +
            "Run the tests from a repository checkout.");
    }
}
