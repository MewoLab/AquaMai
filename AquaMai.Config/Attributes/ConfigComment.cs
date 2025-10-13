using System;
using System.Collections.Generic;
using AquaMai.Config.Interfaces;

namespace AquaMai.Config.Attributes;

public record ConfigComment(string CommentEn, string CommentZh, string NameZh) : IConfigComment
{
    public string GetLocalized(string lang)
    {
        switch (lang)
        {
            case "en":
                return CommentEn ?? "";
            case "zh":
                List<string> lines = new();
                if (!string.IsNullOrEmpty(NameZh))
                    lines.Add(NameZh);
                if (!string.IsNullOrEmpty(CommentZh))
                    lines.Add(CommentZh);
                return string.Join("\n", lines);
            default:
                throw new ArgumentException($"Unsupported language: {lang}");
        }
    }
}
