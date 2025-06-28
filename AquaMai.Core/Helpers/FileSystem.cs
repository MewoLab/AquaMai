using System.IO;

namespace AquaMai.Core.Helpers;

public static class FileSystem
{
    public static string ResolvePath(string path)
    {
        var varExpanded = System.Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(varExpanded)
                 ? varExpanded
                 : Path.Combine(System.Environment.CurrentDirectory, varExpanded);
    }
}
