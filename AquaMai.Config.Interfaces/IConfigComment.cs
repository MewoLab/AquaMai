namespace AquaMai.Config.Interfaces;

public interface IConfigComment
{
    string CommentEn { get; init; }
    string CommentZh { get; init; }
    // 为了使旧版 MCM 能编辑新版 AquaMai 的配置，这里不能加 init;
    // 如果加了，旧版 MCM 会因为构造函数执行时找不到接口里的 setter 定义而报错（我也不知道为什么 C# 在执行时还会检查接口）
    // 然而，前两个已经加了。如果再删掉的话，也会炸掉。现在能保持最佳兼容性的方法就是以后新增的东西都不需要 init; IConfigSectionAttribute 里就都是 get;
    // 接口里如果不写的话，就不会序列化了
    string NameZh { get; }
    public string GetLocalized(string lang);
}
