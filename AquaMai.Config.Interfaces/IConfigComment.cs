namespace AquaMai.Config.Interfaces;

public interface IConfigComment
{
    string CommentEn { get; init; }
    string CommentZh { get; init; }
    string NameZh { get; init; }
    public string GetLocalized(string lang);
    public string GetLocalizedForComment(string lang);
}
