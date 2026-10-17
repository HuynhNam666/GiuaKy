using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace _2380601390.Exams.Attempts;

public class PlannedOption
{
    public long QuestionOptionId { get; set; }
    public int DisplayOrder { get; set; }
}

public class PlannedQuestion
{
    public long QuestionId { get; set; }
    public int DisplayOrder { get; set; }
    public List<PlannedOption> Options { get; set; } = new List<PlannedOption>();
}

public class PlannedGroup
{
    public long ReadingGroupId { get; set; }
    public int DisplayOrder { get; set; }
}

public class AttemptPlan
{
    public List<PlannedGroup> Groups { get; set; } = new List<PlannedGroup>();
    public List<PlannedQuestion> Questions { get; set; } = new List<PlannedQuestion>();
}

/// <summary>
/// Sinh thứ tự trình bày cho một lượt thi (chạy một lần lúc bắt đầu; kết quả được lưu vào CSDL).
/// Quy tắc: không phá ranh giới Part (5 → 6 → 7); không tách câu khỏi nhóm bài đọc;
/// Part 6 luôn giữ thứ tự câu trong nhóm; chỉ trộn ở mức các cờ trong đề cho phép.
/// Cần nạp sẵn Parts, Groups, Questions, Options của phiên bản.
/// </summary>
public static class AttemptShuffler
{
    public static AttemptPlan Plan(ExamVersion version)
    {
        var plan = new AttemptPlan();
        var order = 0;
        var groupOrder = 0;

        foreach (var part in version.Parts.OrderBy(p => p.DisplayOrder))
        {
            if (part.Groups.Count == 0)
            {
                // Câu độc lập (Part 5).
                var questions = part.Questions.Where(q => q.ReadingGroupId == null).OrderBy(q => q.SourceOrder).ToList();
                if (part.ShuffleItems) Shuffle(questions);
                foreach (var q in questions) plan.Questions.Add(PlanQuestion(q, ++order));
                continue;
            }

            var groups = part.Groups.OrderBy(g => g.DisplayOrder).ToList();
            if (part.ShuffleItems) Shuffle(groups);

            foreach (var g in groups)
            {
                plan.Groups.Add(new PlannedGroup { ReadingGroupId = g.Id, DisplayOrder = ++groupOrder });

                var qs = part.Questions.Where(q => q.ReadingGroupId == g.Id).OrderBy(q => q.SourceOrder).ToList();
                if (g.ShuffleQuestions && part.PartNo != 6) Shuffle(qs);
                foreach (var q in qs) plan.Questions.Add(PlanQuestion(q, ++order));
            }
        }

        return plan;
    }

    private static PlannedQuestion PlanQuestion(Question q, int displayOrder)
    {
        var options = q.Options.OrderBy(o => o.SourceOrder).ToList();
        if (q.ShuffleOptions) Shuffle(options);

        var pq = new PlannedQuestion { QuestionId = q.Id, DisplayOrder = displayOrder };
        for (var i = 0; i < options.Count; i++)
        {
            pq.Options.Add(new PlannedOption { QuestionOptionId = options[i].Id, DisplayOrder = i });
        }

        return pq;
    }

    /// <summary>Fisher–Yates với bộ sinh số ngẫu nhiên mật mã (không đoán trước được).</summary>
    public static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
