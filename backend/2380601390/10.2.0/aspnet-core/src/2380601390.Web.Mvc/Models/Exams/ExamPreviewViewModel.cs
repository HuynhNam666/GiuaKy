using System.Linq;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Web.Models.Exams;

public class ExamPreviewViewModel
{
    public ExamPreviewDto Preview { get; set; }

    /// <summary>"p5" hoặc "g{groupId}".</summary>
    public string SelectedKey { get; set; }

    public PartPreviewDto SelectedPart =>
        SelectedGroup != null
            ? Preview.Parts.First(p => p.Groups.Contains(SelectedGroup))
            : Preview.Parts.FirstOrDefault(p => p.PartNo == 5) ?? Preview.Parts.First();

    public GroupPreviewDto SelectedGroup
    {
        get
        {
            if (SelectedKey != null && SelectedKey.StartsWith("g") && long.TryParse(SelectedKey.Substring(1), out var gid))
            {
                return Preview.Parts.SelectMany(p => p.Groups).FirstOrDefault(g => g.Id == gid);
            }

            return null;
        }
    }
}

public class QuestionCardModel
{
    public _2380601390.Exams.Teacher.Dto.QuestionPreviewDto Question { get; set; }
    public bool CanEdit { get; set; }
    public bool IsPart6 { get; set; }

    /// <summary>Nhãn hiển thị trong ngăn kéo sửa (vd. "Part 7 · Nhóm G7-3").</summary>
    public string GroupLabel { get; set; }
}

public static class PreviewText
{
    private static readonly System.Text.RegularExpressions.Regex Blank =
        new System.Text.RegularExpressions.Regex(@"\{\{blank:([A-Za-z0-9_-]+)\}\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Mã hóa HTML rồi làm nổi các chỗ trống Part 6 ({{blank:id}}) và xuống dòng.</summary>
    public static Microsoft.AspNetCore.Html.IHtmlContent Render(string text)
    {
        var enc = System.Net.WebUtility.HtmlEncode(text ?? "");
        enc = Blank.Replace(enc, "<span class=\"toeic-blank\">$1</span>").Replace("\n", "<br/>");
        return new Microsoft.AspNetCore.Html.HtmlString(enc);
    }
}
