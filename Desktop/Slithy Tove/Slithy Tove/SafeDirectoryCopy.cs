namespace Slithy_Tove;

internal static class SafeDirectoryCopy
{
    public static void Copy(string sourceDirectory, string destinationDirectory, bool overwrite)
    {
        string source = Path.GetFullPath(sourceDirectory);
        string destination = Path.GetFullPath(destinationDirectory);
        RejectLinksInPath(source);
        RejectLinksInPath(destination);
        string sourcePrefix = Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar;
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The destination must be outside the folder being copied.");

        // Inspect the complete tree before copying. Recursive enumeration can follow
        // a junction into a different wallet or user folder before a caller notices.
        List<(string Path, bool Directory)> entries = [];
        Inspect(source, entries);
        foreach ((string path, _) in entries)
            RejectLinksInPath(Path.Combine(destination, Path.GetRelativePath(source, path)));

        Directory.CreateDirectory(destination);
        foreach ((string path, bool directory) in entries)
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, path));
            RejectLinksInPath(path);
            RejectLinksInPath(target);
            if (directory) Directory.CreateDirectory(target);
            else File.Copy(path, target, overwrite);
        }
    }

    private static void Inspect(string directory, List<(string Path, bool Directory)> entries)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked files and folders cannot be copied. Choose a local folder without links.");
            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            entries.Add((path, isDirectory));
            if (isDirectory) Inspect(path, entries);
        }
    }

    internal static void RejectLinksInPath(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                // GetAttributes also detects dangling links that Exists reports as missing.
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked files and folders cannot be copied. Choose a local folder without links.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
