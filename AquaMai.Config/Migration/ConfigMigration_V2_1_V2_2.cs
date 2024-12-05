using AquaMai.Config.Interfaces;
using Tomlet.Models;

namespace AquaMai.Config.Migration;

public class ConfigMigration_V2_1_V2_2 : IConfigMigration
{
    public string FromVersion => "2.1";
    public string ToVersion => "2.2";

    public ConfigView Migrate(ConfigView src)
    {
        var dst = (ConfigView)src.Clone();
        dst.SetValue("Version", ToVersion);

        if (src.IsSectionEnabled("GameSystem.Assets.UseJacketAsDummyMovie"))
        {
            dst.Remove("GameSystem.Assets.UseJacketAsDummyMovie");
            dst.SetValue("GameSystem.Assets.MovieLoader.JacketAsMovie", true);
        }

        if (src.TryGetValue<string>("GameSystem.Assets.LoadLocalImages.LocalAssetsDir", out var localAssetsDir))
        {
            dst.SetValue("GameSystem.Assets.LoadLocalImages.ImageAssetsDir", localAssetsDir);
            dst.Remove("GameSystem.Assets.LoadLocalImages.LocalAssetsDir");
        }

        return dst;
    }
}