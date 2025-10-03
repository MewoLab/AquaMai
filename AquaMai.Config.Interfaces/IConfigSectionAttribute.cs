namespace AquaMai.Config.Interfaces;

public interface IConfigSectionAttribute
{
    IConfigComment Comment { get; }
    bool ExampleHidden { get; }
    bool DefaultOn { get; }
    bool AlwaysEnabled { get; }
    string Name { get; }
}