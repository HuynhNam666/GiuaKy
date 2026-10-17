using System.Collections.Generic;
using System.Linq;

namespace _2380601390.Exams.Import;

/// <summary>
/// Chuyển ParsedExam (trong bộ nhớ) thành đồ thị entity để lưu. Hàm thuần: không truy cập DB,
/// nên toàn bộ đồ thị được lưu bằng đúng một SaveChanges (một transaction).
/// </summary>
public static class ExamGraphBuilder
{
    /// <param name="existingMediaIds">sha256 -> Id của MediaAsset đã có trong DB (tái dùng ảnh trùng).</param>
    public static ExamVersion BuildVersion(
        ParsedExam parsed,
        int versionNo,
        IReadOnlyDictionary<string, long> existingMediaIds,
        string sourceFileName,
        string sourceFileSha256)
    {
        var newAssets = new Dictionary<string, MediaAsset>();
        var version = new ExamVersion
        {
            VersionNo = versionNo,
            Status = ExamVersionStatus.Draft,
            DurationMinutes = parsed.DurationMinutes,
            TotalQuestions = parsed.TotalQuestions,
            TagsVersion = parsed.TagsVersion,
            SourceFileName = sourceFileName,
            SourceFileSha256 = sourceFileSha256
        };

        var partOrder = 0;
        foreach (var pp in parsed.Parts)
        {
            var part = new ExamPart
            {
                ExamVersion = version,
                PartNo = pp.PartNo,
                FromNo = pp.From,
                ToNo = pp.To,
                ShuffleItems = pp.ShuffleItems,
                DisplayOrder = ++partOrder
            };
            version.Parts.Add(part);

            var sourceOrder = 0;
            foreach (var pq in pp.Questions)
            {
                part.Questions.Add(BuildQuestion(pq, version, part, null, ++sourceOrder));
            }

            var groupOrder = 0;
            foreach (var pg in pp.Groups)
            {
                var group = new ReadingGroup
                {
                    ExamPart = part,
                    Code = pg.Code,
                    DisplayOrder = ++groupOrder,
                    ShuffleQuestions = pg.ShuffleQuestions
                };
                part.Groups.Add(group);

                var docOrder = 0;
                foreach (var pd in pg.Documents)
                {
                    var doc = new ReadingDocument
                    {
                        ReadingGroup = group,
                        Code = pd.Code,
                        Title = pd.Title,
                        DisplayOrder = ++docOrder
                    };
                    group.Documents.Add(doc);

                    var blockOrder = 0;
                    foreach (var pb in pd.Blocks)
                    {
                        var block = new ContentBlock
                        {
                            ReadingDocument = doc,
                            DisplayOrder = ++blockOrder,
                            BlockType = pb.Type
                        };

                        if (pb.Type == ContentBlockType.Text)
                        {
                            block.Text = pb.Text;
                        }
                        else
                        {
                            AttachMedia(block, pb.Image, existingMediaIds, newAssets);
                        }

                        doc.Blocks.Add(block);
                    }
                }

                var qOrder = 0;
                foreach (var pq in pg.Questions)
                {
                    // Câu thuộc nhóm: nằm trong part.Questions (cùng ExamPart) và trỏ tới nhóm qua ReadingGroup.
                    part.Questions.Add(BuildQuestion(pq, version, part, group, ++qOrder));
                }
            }
        }

        return version;
    }

    private static void AttachMedia(ContentBlock block, ParsedImage img, IReadOnlyDictionary<string, long> existing, Dictionary<string, MediaAsset> created)
    {
        block.AltText = img.AltText;
        if (existing.TryGetValue(img.Sha256, out var id))
        {
            block.MediaAssetId = id;
            return;
        }

        if (!created.TryGetValue(img.Sha256, out var asset))
        {
            asset = new MediaAsset
            {
                Sha256 = img.Sha256,
                ContentType = img.ContentType,
                SizeBytes = img.Data.LongLength,
                Data = img.Data
            };
            created[img.Sha256] = asset;
        }

        block.MediaAsset = asset;
    }

    private static Question BuildQuestion(ParsedQuestion pq, ExamVersion version, ExamPart part, ReadingGroup group, int sourceOrder)
    {
        var q = new Question
        {
            ExamVersion = version,
            ExamPart = part,
            ReadingGroup = group,
            Code = pq.Code,
            SourceNo = pq.No,
            SourceOrder = sourceOrder,
            StemText = string.IsNullOrWhiteSpace(pq.Stem) ? null : pq.Stem,
            Explanation = pq.Explanation,
            ShuffleOptions = pq.ShuffleOptions
        };

        var order = 0;
        foreach (var po in pq.Options)
        {
            q.Options.Add(new QuestionOption { Question = q, Code = po.Code, Text = po.Text, SourceOrder = ++order });
        }

        return q;
    }

    /// <summary>
    /// Lưu đồ thị theo hai pha. EF không sắp xếp được thứ tự INSERT khi Question.CorrectOption trỏ tới
    /// QuestionOption mà QuestionOption lại thuộc Question (vòng phụ thuộc), nên: pha 1 lưu toàn bộ đồ thị,
    /// pha 2 gán CorrectOptionId theo [ANSWER]. Hai pha PHẢI chạy trong cùng một transaction do bên gọi quản lý
    /// (Unit of Work của ABP, hoặc BeginTransaction khi dùng DbContext trực tiếp) để vẫn "lưu hết hoặc không lưu gì".
    /// </summary>
    public static async System.Threading.Tasks.Task PersistAsync(
        ExamVersion version, ParsedExam parsed, System.Func<System.Threading.Tasks.Task> saveChanges)
    {
        await saveChanges();

        var answers = parsed.AllQuestions.ToDictionary(q => q.Code, q => q.AnswerRef);
        foreach (var q in version.Parts.SelectMany(p => p.Questions))
        {
            q.CorrectOptionId = q.Options.Single(o => o.Code == answers[q.Code]).Id;
        }

        await saveChanges();
    }
}
