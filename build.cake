#addin nuget:?package=Cake.Git&version=5.0.1
#addin nuget:?package=Cake.FileHelpers&version=7.0.0

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");

Task("Restore")
    .Does(() =>
{
    // 运行 dotnet restore
    DotNetRestore("./AquaMai.sln");
});

Task("PreBuild")
    .Does(() =>
{
    var gitDescribe = GitDescribe(".", GitDescribeStrategy.Tags).Substring(1); // 获取 git describe 的输出
    var buildDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    var shortVers = gitDescribe.Split('-');
    string shortVer;
    if (shortVers.Length > 1)
    {
        shortVer = $"{shortVers[0]}.{shortVers[1]}";
    }
    else
    {
        shortVer = shortVers[0];
    }

    var versionContent = $@"
    // Auto-generated file. Do not modify manually.
    namespace AquaMai;

    public static partial class BuildInfo
    {{
        public const string Version = ""{shortVer}"";
        public const string GitVersion = ""{gitDescribe}"";
        public const string BuildDate = ""{buildDate}"";
    }}
    ";
    FileWriteText("./AquaMai/BuildInfo.g.cs", versionContent);
});

Task("Build")
    .IsDependentOn("PreBuild")
    .IsDependentOn("Restore")
    .Does(() =>
{
    // 使用 dotnet build 进行构建
    DotNetBuild("./AquaMai.sln", new DotNetBuildSettings
    {
        Configuration = configuration
    });
});

RunTarget(target);
