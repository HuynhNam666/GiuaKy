using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace _2380601390.Exams;

/// <summary>
/// Nhân bản sâu một phiên bản (để sửa: bản đã xuất bản bất biến, bản sửa là một Nháp mới).
/// Ảnh (MediaAsset) được dùng chung, không sao chép dữ liệu.
/// Cần nạp sẵn Parts, Groups, Documents, Blocks, Questions, Options của bản nguồn.
/// </summary>
public class ExamVersionClone
{
    public ExamVersion Version { get; set; }

    /// <summary>Câu mới -> mã đáp án đúng (A–D) của câu gốc, để gán CorrectOptionId sau khi lưu.</summary>
    public Dictionary<Question, string> CorrectCodes { get; set; } = new Dictionary<Question, string>();
}

public static class ExamVersionCloner
{
    public static ExamVersionClone Clone(ExamVersion src, int newVersionNo)
    {
        var result = new ExamVersionClone();
        var version = new ExamVersion
        {
            ExamId = src.ExamId,
            VersionNo = newVersionNo,
            Status = ExamVersionStatus.Draft,
            DurationMinutes = src.DurationMinutes,
            TotalQuestions = src.TotalQuestions,
            TagsVersion = src.TagsVersion,
            SourceFileName = src.SourceFileName,
            SourceFileSha256 = src.SourceFileSha256
        };
        result.Version = version;

        foreach (var sp in src.Parts.OrderBy(p => p.DisplayOrder))
        {
            var part = new ExamPart
            {
                ExamVersion = version,
                PartNo = sp.PartNo,
                FromNo = sp.FromNo,
                ToNo = sp.ToNo,
                ShuffleItems = sp.ShuffleItems,
                DisplayOrder = sp.DisplayOrder
            };
            version.Parts.Add(part);

            var groupMap = new Dictionary<long, ReadingGroup>();
            foreach (var sg in sp.Groups.OrderBy(g => g.DisplayOrder))
            {
                var group = new ReadingGroup
                {
                    ExamPart = part,
                    Code = sg.Code,
                    DisplayOrder = sg.DisplayOrder,
                    ShuffleQuestions = sg.ShuffleQuestions
                };
                part.Groups.Add(group);
                groupMap[sg.Id] = group;

                foreach (var sd in sg.Documents.OrderBy(d => d.DisplayOrder))
                {
                    var doc = new ReadingDocument { ReadingGroup = group, Code = sd.Code, Title = sd.Title, DisplayOrder = sd.DisplayOrder };
                    group.Documents.Add(doc);
                    foreach (var sb in sd.Blocks.OrderBy(b => b.DisplayOrder))
                    {
                        doc.Blocks.Add(new ContentBlock
                        {
                            ReadingDocument = doc,
                            DisplayOrder = sb.DisplayOrder,
                            BlockType = sb.BlockType,
                            Text = sb.Text,
                            MediaAssetId = sb.MediaAssetId,
                            AltText = sb.AltText
                        });
                    }
                }
            }

            foreach (var sq in sp.Questions.OrderBy(q => q.ReadingGroupId ?? 0).ThenBy(q => q.SourceOrder))
            {
                var q = new Question
                {
                    ExamVersion = version,
                    ExamPart = part,
                    ReadingGroup = sq.ReadingGroupId.HasValue ? groupMap[sq.ReadingGroupId.Value] : null,
                    Code = sq.Code,
                    SourceNo = sq.SourceNo,
                    SourceOrder = sq.SourceOrder,
                    StemText = sq.StemText,
                    Explanation = sq.Explanation,
                    ShuffleOptions = sq.ShuffleOptions
                };
                foreach (var so in sq.Options.OrderBy(o => o.SourceOrder))
                {
                    q.Options.Add(new QuestionOption { Question = q, Code = so.Code, Text = so.Text, SourceOrder = so.SourceOrder });
                }

                var correct = sq.Options.FirstOrDefault(o => o.Id == sq.CorrectOptionId);
                if (correct != null) result.CorrectCodes[q] = correct.Code;
                part.Questions.Add(q);
            }
        }

        return result;
    }

    /// <summary>
    /// Lưu hai pha (xem ExamGraphBuilder.PersistAsync): bên gọi phải bao cả hai pha trong một transaction.
    /// </summary>
    public static async Task PersistAsync(ExamVersionClone clone, Func<Task> saveChanges)
    {
        await saveChanges();
        foreach (var kv in clone.CorrectCodes)
        {
            kv.Key.CorrectOptionId = kv.Key.Options.Single(o => o.Code == kv.Value).Id;
        }

        await saveChanges();
    }
}
