using System.Linq;
using _2380601390.Exams.Import;

namespace _2380601390.Exams;

/// <summary>
/// Chuyển phiên bản đã lưu (entity) về ParsedExam để dùng lại đúng ExamValidator như lúc import,
/// nhờ vậy "kiểm tra lại sau khi sửa" và "kiểm tra trước khi xuất bản" dùng cùng một bộ quy tắc.
/// Cần nạp sẵn: Exam, Parts, Groups, Documents, Blocks, Questions, Options.
/// </summary>
public static class ExamVersionMapper
{
    public static ParsedExam ToParsed(ExamVersion v)
    {
        var exam = new ParsedExam
        {
            Id = v.Exam?.Code,
            Title = v.Exam?.Title,
            DurationMinutes = v.DurationMinutes,
            TotalQuestions = v.TotalQuestions,
            TagsVersion = v.TagsVersion
        };

        foreach (var part in v.Parts.OrderBy(p => p.DisplayOrder))
        {
            var pp = new ParsedPart
            {
                PartNo = part.PartNo,
                From = part.FromNo,
                To = part.ToNo,
                ShuffleItems = part.ShuffleItems
            };

            pp.Questions.AddRange(part.Questions
                .Where(q => q.ReadingGroupId == null)
                .OrderBy(q => q.SourceOrder)
                .Select(MapQuestion));

            foreach (var g in part.Groups.OrderBy(x => x.DisplayOrder))
            {
                var pg = new ParsedGroup { Code = g.Code, ShuffleQuestions = g.ShuffleQuestions };
                foreach (var d in g.Documents.OrderBy(x => x.DisplayOrder))
                {
                    var pd = new ParsedDocument { Code = d.Code, Title = d.Title };
                    foreach (var b in d.Blocks.OrderBy(x => x.DisplayOrder))
                    {
                        pd.Blocks.Add(new ParsedBlock
                        {
                            Type = b.BlockType,
                            Text = b.Text,
                            Image = b.MediaAssetId.HasValue || b.MediaAsset != null ? new ParsedImage() : null
                        });
                    }

                    pg.Documents.Add(pd);
                }

                pg.Questions.AddRange(part.Questions
                    .Where(q => q.ReadingGroupId == g.Id)
                    .OrderBy(q => q.SourceOrder)
                    .Select(MapQuestion));
                pp.Groups.Add(pg);
            }

            exam.Parts.Add(pp);
        }

        return exam;
    }

    private static ParsedQuestion MapQuestion(Question q) => new ParsedQuestion
    {
        Code = q.Code,
        No = q.SourceNo,
        ShuffleOptions = q.ShuffleOptions,
        Stem = q.StemText ?? "",
        Explanation = q.Explanation,
        AnswerRef = q.Options.FirstOrDefault(o => o.Id == q.CorrectOptionId)?.Code,
        Options = q.Options.OrderBy(o => o.SourceOrder).Select(o => new ParsedOption { Code = o.Code, Text = o.Text }).ToList()
    };
}
