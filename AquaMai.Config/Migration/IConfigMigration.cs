using AquaMai.Config.Interfaces;

namespace AquaMai.Config.Migration;

public interface IConfigMigration
{
    public string FromVersion { get; }
    public string ToVersion { get; }
    public ConfigView Migrate(ConfigView config);
}
