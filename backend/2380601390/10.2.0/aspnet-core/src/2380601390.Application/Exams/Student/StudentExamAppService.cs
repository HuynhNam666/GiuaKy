using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using _2380601390.Authorization;
using _2380601390.Exams.Attempts;
using _2380601390.Exams.Student.Dto;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.Exams.Student;

public interface IStudentExamAppService : Abp.Application.Services.IApplicationService
{
    Task<List<PublishedExamDto>> GetPublishedAsync();
    Task<AttemptDto> StartAsync(StartAttemptInput input);
    Task<AttemptDto> GetAsync(AttemptIdInput input);
    Task<SaveAnswerResultDto> SaveAnswerAsync(SaveAnswerInput input);
    Task<AttemptSummaryDto> SubmitAsync(AttemptIdInput input);
    Task<AttemptResultDto> GetResultAsync(AttemptIdInput input);
    Task<List<AttemptSummaryDto>> GetMyResultsAsync();
}

/// <summary>
/// API cho sinh viên (mobile-web): chỉ đề ĐÃ XUẤT BẢN; chỉ lượt thi của chính mình.
/// Trong lúc làm bài không trả đáp án đúng/giải thích; chỉ GetResult (sau khi nộp) mới có.
/// </summary>
[AbpAuthorize(PermissionNames.Pages_Attempts)]
public class StudentExamAppService : _2380601390AppServiceBase, IStudentExamAppService
{
    private static readonly Regex BlankRx = new Regex(@"\{\{blank:([A-Za-z0-9_-]+)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] Labels = { "A", "B", "C", "D" };

    private readonly IRepository<ExamVersion, long> _versions;
    private readonly IRepository<ExamAttempt, long> _attempts;
    private readonly IRepository<AttemptQuestion, long> _attemptQuestions;
    private readonly IRepository<AttemptGroup, long> _attemptGroups;
    private readonly IRepository<Question, long> _questions;
    private readonly ExamAttemptManager _manager;

    public StudentExamAppService(
        IRepository<ExamVersion, long> versions,
        IRepository<ExamAttempt, long> attempts,
        IRepository<AttemptQuestion, long> attemptQuestions,
        IRepository<AttemptGroup, long> attemptGroups,
        IRepository<Question, long> questions,
        ExamAttemptManager manager)
    {
        _versions = versions;
        _attempts = attempts;
        _attemptQuestions = attemptQuestions;
        _attemptGroups = attemptGroups;
        _questions = questions;
        _manager = manager;
    }

    private long UserId => AbpSession.GetUserId();

    // ---------- danh sách đề ----------

    public async Task<List<PublishedExamDto>> GetPublishedAsync()
    {
        var uid = UserId;
        var now = DateTime.UtcNow;

        // Thu các lượt đã quá hạn của sinh viên này (kể cả khi họ đã đóng app).
        var expired = await _attempts.GetAll().AsNoTracking()
            .Where(a => a.UserId == uid && a.Status == AttemptStatus.InProgress && a.ExpiresAtUtc <= now)
            .Select(a => a.Id).ToListAsync();
        foreach (var id in expired) await _manager.FinalizeAsync(id);

        var exams = await _versions.GetAll().AsNoTracking()
            .Where(v => v.Status == ExamVersionStatus.Published)
            .Select(v => new PublishedExamDto
            {
                ExamVersionId = v.Id,
                Title = v.Exam.Title,
                DurationMinutes = v.DurationMinutes,
                TotalQuestions = v.TotalQuestions
            })
            .OrderBy(v => v.Title)
            .ToListAsync();

        var mine = await _attempts.GetAll().AsNoTracking()
            .Where(a => a.UserId == uid)
            .Select(a => new { a.Id, a.ExamVersionId, a.Status, a.ExpiresAtUtc, a.StartedAtUtc, a.ScorePercent })
            .ToListAsync();

        foreach (var e in exams)
        {
            var running = mine.FirstOrDefault(a => a.ExamVersionId == e.ExamVersionId && a.Status == AttemptStatus.InProgress);
            if (running != null)
            {
                e.InProgressAttemptId = running.Id;
                e.InProgressExpiresAtUtc = running.ExpiresAtUtc;
            }

            var last = mine.Where(a => a.ExamVersionId == e.ExamVersionId && a.Status != AttemptStatus.InProgress)
                .OrderByDescending(a => a.StartedAtUtc).FirstOrDefault();
            if (last != null)
            {
                e.LastAttemptId = last.Id;
                e.LastScorePercent = last.ScorePercent;
            }
        }

        return exams;
    }

    // ---------- làm bài ----------

    public async Task<AttemptDto> StartAsync(StartAttemptInput input)
    {
        var attempt = await _manager.StartOrResumeAsync(UserId, input.ExamVersionId);
        return await BuildAttemptDtoAsync(attempt.Id);
    }

    public async Task<AttemptDto> GetAsync(AttemptIdInput input)
    {
        var a = await _manager.GetOwnedAsync(UserId, input.AttemptId);
        if (a.Status == AttemptStatus.InProgress && DateTime.UtcNow >= a.ExpiresAtUtc)
        {
            await _manager.FinalizeAsync(a.Id); // hết hạn: thu bài ngay khi phát hiện
        }

        return await BuildAttemptDtoAsync(input.AttemptId);
    }

    public async Task<SaveAnswerResultDto> SaveAnswerAsync(SaveAnswerInput input)
    {
        var outcome = await _manager.SaveAnswerAsync(UserId, input.AttemptId, input.QuestionId, input.OptionId, input.Revision);
        var a = await _manager.GetOwnedAsync(UserId, input.AttemptId);
        var now = DateTime.UtcNow;
        return new SaveAnswerResultDto
        {
            Saved = outcome.Saved,
            Revision = outcome.Revision,
            SelectedOptionId = outcome.SelectedOptionId,
            ServerNowUtc = now,
            RemainingSeconds = Remaining(a, now)
        };
    }

    public async Task<AttemptSummaryDto> SubmitAsync(AttemptIdInput input)
    {
        var a = await _manager.SubmitAsync(UserId, input.AttemptId);
        return ToSummary(a);
    }

    // ---------- kết quả ----------

    public async Task<AttemptResultDto> GetResultAsync(AttemptIdInput input)
    {
        var a = await _manager.GetOwnedAsync(UserId, input.AttemptId);
        if (a.Status == AttemptStatus.InProgress && DateTime.UtcNow >= a.ExpiresAtUtc)
        {
            await _manager.FinalizeAsync(a.Id);
            a = await _manager.GetOwnedAsync(UserId, input.AttemptId);
        }

        if (a.Status == AttemptStatus.InProgress)
            throw new UserFriendlyException(AttemptErrorCodes.NotSubmitted, "Chưa nộp bài nên chưa xem được kết quả.");

        var rows = await _attemptQuestions.GetAll().AsNoTracking()
            .Where(x => x.ExamAttemptId == a.Id)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new
            {
                x.DisplayOrder,
                x.SelectedOptionId,
                x.IsCorrect,
                PartNo = x.Question.ExamPart.PartNo,
                x.Question.ReadingGroupId,
                Stem = x.Question.StemText,
                x.Question.Explanation,
                CorrectId = x.Question.CorrectOptionId,
                Opts = x.Options.OrderBy(o => o.DisplayOrder).Select(o => new { o.QuestionOptionId, o.DisplayOrder, o.QuestionOption.Text }).ToList()
            })
            .ToListAsync();

        var dto = new AttemptResultDto();
        FillSummary(dto, a);
        dto.Questions = rows.Select(r => new ResultQuestionDto
        {
            Number = r.DisplayOrder,
            PartNo = r.PartNo,
            GroupId = r.ReadingGroupId,
            Stem = r.Stem,
            Explanation = r.Explanation,
            Outcome = r.SelectedOptionId == null ? "Skipped" : r.IsCorrect == true ? "Correct" : "Wrong",
            Options = r.Opts.Select(o => new ResultOptionDto
            {
                OptionId = o.QuestionOptionId,
                Label = Labels[Math.Min(o.DisplayOrder, 3)],
                Text = o.Text,
                IsCorrect = o.QuestionOptionId == r.CorrectId,
                IsSelected = o.QuestionOptionId == r.SelectedOptionId
            }).ToList()
        }).ToList();

        dto.Parts = rows.GroupBy(r => r.PartNo).OrderBy(g => g.Key)
            .Select(g => new PartScoreDto { PartNo = g.Key, Total = g.Count(), Correct = g.Count(r => r.IsCorrect == true) }).ToList();
        return dto;
    }

    public async Task<List<AttemptSummaryDto>> GetMyResultsAsync()
    {
        var uid = UserId;
        var list = await _attempts.GetAll().AsNoTracking()
            .Include(a => a.ExamVersion).ThenInclude(v => v.Exam)
            .Where(a => a.UserId == uid && a.Status != AttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAtUtc)
            .ToListAsync();
        return list.Select(ToSummary).ToList();
    }

    // ---------- dựng DTO ----------

    private static int Remaining(ExamAttempt a, DateTime now)
    {
        if (a.Status != AttemptStatus.InProgress) return 0;
        return (int)Math.Max(0, Math.Floor((a.ExpiresAtUtc - now).TotalSeconds));
    }

    private static void FillSummary(AttemptSummaryDto d, ExamAttempt a)
    {
        d.AttemptId = a.Id;
        d.ExamVersionId = a.ExamVersionId;
        d.Title = a.ExamVersion?.Exam?.Title;
        d.Status = a.Status;
        d.StartedAtUtc = a.StartedAtUtc;
        d.SubmittedAtUtc = a.SubmittedAtUtc;
        d.TotalQuestions = a.TotalQuestions;
        d.CorrectCount = a.CorrectCount ?? 0;
        d.WrongCount = a.WrongCount ?? 0;
        d.SkippedCount = a.SkippedCount ?? 0;
        d.ScorePercent = a.ScorePercent ?? 0;
    }

    private static AttemptSummaryDto ToSummary(ExamAttempt a)
    {
        var d = new AttemptSummaryDto();
        FillSummary(d, a);
        return d;
    }

    /// <summary>Chỉ chọn các cột an toàn (không có CorrectOptionId/Explanation/Code) nên không thể lộ đáp án.</summary>
    private async Task<AttemptDto> BuildAttemptDtoAsync(long attemptId)
    {
        var a = await _manager.GetOwnedAsync(UserId, attemptId);
        var now = DateTime.UtcNow;

        var dto = new AttemptDto
        {
            AttemptId = a.Id,
            ExamVersionId = a.ExamVersionId,
            Title = a.ExamVersion.Exam.Title,
            Status = a.Status,
            TotalQuestions = a.TotalQuestions,
            StartedAtUtc = a.StartedAtUtc,
            ExpiresAtUtc = a.ExpiresAtUtc,
            ServerNowUtc = now,
            RemainingSeconds = Remaining(a, now)
        };

        var qs = await _attemptQuestions.GetAll().AsNoTracking()
            .Where(x => x.ExamAttemptId == attemptId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new
            {
                x.QuestionId,
                x.DisplayOrder,
                x.SelectedOptionId,
                x.AnswerRevision,
                Stem = x.Question.StemText,
                PartNo = x.Question.ExamPart.PartNo,
                GroupId = x.Question.ReadingGroupId,
                Opts = x.Options.OrderBy(o => o.DisplayOrder).Select(o => new { o.QuestionOptionId, o.DisplayOrder, o.QuestionOption.Text }).ToList()
            })
            .ToListAsync();

        dto.Questions = qs.Select(q => new AttemptQuestionDto
        {
            Number = q.DisplayOrder,
            QuestionId = q.QuestionId,
            PartNo = q.PartNo,
            GroupId = q.GroupId,
            Stem = q.Stem,
            SelectedOptionId = q.SelectedOptionId,
            Revision = q.AnswerRevision,
            Options = q.Opts.Select(o => new AttemptOptionDto
            {
                OptionId = o.QuestionOptionId,
                Label = Labels[Math.Min(o.DisplayOrder, 3)],
                Text = o.Text
            }).ToList()
        }).ToList();

        // Part 6: đổi {{blank:mã-câu}} thành {{blank:số-thứ-tự-hiển-thị}} để sinh viên thấy đúng số câu đã trộn.
        var groupQuestionIds = qs.Where(q => q.GroupId != null).Select(q => q.QuestionId).ToList();
        var codeToNumber = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (groupQuestionIds.Count > 0)
        {
            var codes = await _questions.GetAll().AsNoTracking()
                .Where(q => groupQuestionIds.Contains(q.Id))
                .Select(q => new { q.Id, q.Code })
                .ToListAsync();
            var numberByQuestion = qs.ToDictionary(q => q.QuestionId, q => q.DisplayOrder);
            foreach (var c in codes) codeToNumber[c.Code] = numberByQuestion[c.Id];
        }

        var groups = await _attemptGroups.GetAll().AsNoTracking()
            .Where(g => g.ExamAttemptId == attemptId)
            .OrderBy(g => g.DisplayOrder)
            .Select(g => new
            {
                g.ReadingGroupId,
                g.DisplayOrder,
                PartNo = g.ReadingGroup.ExamPart.PartNo,
                Docs = g.ReadingGroup.Documents.OrderBy(d => d.DisplayOrder).Select(d => new
                {
                    Blocks = d.Blocks.OrderBy(b => b.DisplayOrder).Select(b => new
                    {
                        b.BlockType,
                        b.Text,
                        Sha = b.MediaAsset != null ? b.MediaAsset.Sha256 : null,
                        b.AltText
                    }).ToList()
                }).ToList()
            })
            .ToListAsync();

        dto.Groups = groups.Select(g => new AttemptGroupDto
        {
            GroupId = g.ReadingGroupId,
            DisplayOrder = g.DisplayOrder,
            PartNo = g.PartNo,
            Documents = g.Docs.Select(d => new AttemptDocumentDto
            {
                Blocks = d.Blocks.Select(b => new AttemptBlockDto
                {
                    Type = b.BlockType == ContentBlockType.Image ? "Image" : "Text",
                    Text = b.Text == null ? null : BlankRx.Replace(b.Text, m =>
                        codeToNumber.TryGetValue(m.Groups[1].Value, out var n) ? "{{blank:" + n + "}}" : m.Value),
                    MediaSha256 = b.Sha,
                    AltText = b.AltText
                }).ToList()
            }).ToList()
        }).ToList();

        return dto;
    }
}
