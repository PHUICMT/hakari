namespace Hakari.Core.Configuration;

public static class HakariPaths
{
    public const string ApplicationFolderName = "Hakari";
    public const string IndexFileName = "index.db";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationFolderName);

    public static string DefaultIndexPath => Path.Combine(DataDirectory, IndexFileName);
}
