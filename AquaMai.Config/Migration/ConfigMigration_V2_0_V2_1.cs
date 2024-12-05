using AquaMai.Config.Interfaces;
using Tomlet.Models;

namespace AquaMai.Config.Migration;

public class ConfigMigration_V2_0_V2_1 : IConfigMigration
{
    public string FromVersion => "2.0";
    public string ToVersion => "2.1";

    public ConfigView Migrate(ConfigView src)
    {
        var dst = (ConfigView)src.Clone();
        dst.SetValue("Version", ToVersion);

        if (src.IsSectionEnabled("Tweaks.ResetTouchAfterTrack"))
        {
            dst.Remove("Tweaks.ResetTouchAfterTrack");
            dst.SetValue("Tweaks.ResetTouch.AfterTrack", true);
        }

        return dst;
    }
}
