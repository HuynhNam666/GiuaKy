using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Abp.UI;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.Exams.Attempts;

public static class AttemptErrorCodes
{
    public const int Expired = 4101;
    public const int AlreadySubmitted = 4102;
    public const int NotStarted = 4103;
    public const int NotPublished = 4104;
    public const int InvalidOption = 4105;
    public const int NotSubmitted = 4106;
}

public class SaveAnswerOutcome
{
    /// <summary>true: máy chủ đã ghi (hoặc đã có đúng giá trị này). false: yêu cầu cũ hơn bản đã lưu nên bị bỏ qua.</summary>
    public bool Saved { get; set; }
    public int Revision { get; set; }
    public long? SelectedOptionId { get; set; }
}

/// <summary>
/// Vòng đời lượt thi: bắt đầu/tiếp tục, lưu đáp án, nộp (idempotent), chấm, tự thu bài hết giờ.
/// Mọi thao tác kiểm tra quyền sở hữu theo userId; các cập nhật quan trọng là một câu lệnh UPDATE có điều kiện
/// (trạng thái, hạn, revision) nên an toàn khi nhiều yêu cầu chạy đồng thời.
/// </summary>
public class ExamAttemptManager : DomainService
{
    private readonly IRepository<ExamAttempt, long> _attempts;
    private readonly IRepository<AttemptQuestion, long> _attemptQuestions;
    private readonly IRepository<AttemptOption, long> _attemptOptions;
    private readonly ExamVersionManager _versionManager;

    public ExamAttemptManager(
        IRepository<ExamAttempt, long> attempts,
        IRepository<AttemptQuestion, long> attemptQuestions,
        IRepository<AttemptOption, long> attemptOptions,
        ExamVersionManager versionManager)
    {
        _attempts = attempts;
        _attemptQuestions = attemptQuestions;
        _attemptOptions = attemptOptions;
        _versionManager = versionManager;
    }

    // ---------- đọc (kiểm tra quyền sở hữu) ----------

    /// <summary>Lượt thi của chính người dùng; lượt của người khác trả về "không tồn tại" (không lộ sự tồn tại).</summary>
    public async Task<ExamAttempt> GetOwnedAsync(long userId, long attemptId)
    {
        var a = await _attempts.GetAll().AsNoTracking()
            .Include(x => x.ExamVersion).ThenInclude(v => v.Exam)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId);
        if (a == null) throw new EntityNotFoundException(typeof(ExamAttempt), attemptId);
        return a;
    }

    // ---------- bắt đầu / tiếp tục ----------

    public async Task<ExamAttempt> StartOrResumeAsync(long userId, long versionId)
    {
        var now = DateTime.UtcNow;

        var existing = await _attempts.GetAll().AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ExamVersionId == versionId && a.Status == AttemptStatus.InProgress);
        if (existing != null)
        {
            if (existing.ExpiresAtUtc > now) return existing; // tiếp tục đúng lượt cũ, giữ nguyên thứ tự và hạn
            await FinalizeAsync(existing.Id);                  // hết hạn khi client đã đóng: thu bài rồi cho làm lượt mới
        }

        var version = await _versionManager.FullQuery().FirstOrDefaultAsync(v => v.Id == versionId);
        if (version == null || version.Status != ExamVersionStatus.Published)
            throw new UserFriendlyException(AttemptErrorCodes.NotPublished, "Đề chưa được xuất bản hoặc đã dừng nhận thi.");

        var plan = AttemptShuffler.Plan(version);
        var attempt = new ExamAttempt
        {
            UserId = userId,
            ExamVersionId = version.Id,
            Status = AttemptStatus.InProgress,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(version.DurationMinutes),
            TotalQuestions = plan.Questions.Count
        };

        foreach (var g in plan.Groups)
        {
            attempt.Groups.Add(new AttemptGroup { ReadingGroupId = g.ReadingGroupId, DisplayOrder = g.DisplayOrder });
        }

        foreach (var q in plan.Questions)
        {
            var aq = new AttemptQuestion { QuestionId = q.QuestionId, DisplayOrder = q.DisplayOrder };
            foreach (var o in q.Options)
            {
                aq.Options.Add(new AttemptOption { QuestionOptionId = o.QuestionOptionId, DisplayOrder = o.DisplayOrder });
            }

            attempt.Questions.Add(aq);
        }

        await _attempts.InsertAsync(attempt);
        await CurrentUnitOfWork.SaveChangesAsync();
        return attempt;
    }

    // ---------- lưu / hủy đáp án ----------

    public async Task<SaveAnswerOutcome> SaveAnswerAsync(long userId, long attemptId, long questionId, long? optionId, int revision)
    {
        if (revision <= 0) throw new UserFriendlyException("Revision phải là số nguyên dương.");

        var attempt = await GetOwnedAsync(userId, attemptId);
        await EnsureWritableAsync(attempt);

        var aq = await _attemptQuestions.GetAll().AsNoTracking()
            .Where(x => x.ExamAttemptId == attemptId && x.QuestionId == questionId)
            .Select(x => new { x.Id, x.AnswerRevision, x.SelectedOptionId })
            .FirstOrDefaultAsync();
        if (aq == null) throw new EntityNotFoundException(typeof(AttemptQuestion), questionId);

        if (optionId.HasValue)
        {
            var belongs = await _attemptOptions.GetAll().AnyAsync(o => o.AttemptQuestionId == aq.Id && o.QuestionOptionId == optionId.Value);
            if (!belongs) throw new UserFriendlyException(AttemptErrorCodes.InvalidOption, "Lựa chọn không thuộc câu hỏi này.");
        }

        var now = DateTime.UtcNow;
        var selected = optionId;

        // Một câu UPDATE có điều kiện: chỉ ghi khi lượt còn InProgress, chưa quá hạn và revision mới hơn bản đã lưu.
        // Nhờ vậy yêu cầu đến muộn/sai thứ tự không ghi đè lựa chọn mới bằng lựa chọn cũ, và không thể ghi sau hạn.
        var affected = await _attemptQuestions.GetAll()
            .Where(x => x.Id == aq.Id
                        && x.AnswerRevision < revision
                        && x.ExamAttempt.Status == AttemptStatus.InProgress
                        && x.ExamAttempt.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SelectedOptionId, selected)
                .SetProperty(x => x.AnswerRevision, revision)
                .SetProperty(x => x.AnsweredAtUtc, (DateTime?)now));

        if (affected == 1)
        {
            return new SaveAnswerOutcome { Saved = true, Revision = revision, SelectedOptionId = optionId };
        }

        // Không ghi được: do hết hạn/đã nộp xen giữa, hoặc do revision cũ.
        var fresh = await GetOwnedAsync(userId, attemptId);
        await EnsureWritableAsync(fresh);

        var current = await _attemptQuestions.GetAll().AsNoTracking()
            .Where(x => x.Id == aq.Id)
            .Select(x => new { x.AnswerRevision, x.SelectedOptionId })
            .FirstAsync();
        var sameAsStored = current.AnswerRevision == revision && current.SelectedOptionId == optionId;
        return new SaveAnswerOutcome { Saved = sameAsStored, Revision = current.AnswerRevision, SelectedOptionId = current.SelectedOptionId };
    }

    private async Task EnsureWritableAsync(ExamAttempt attempt)
    {
        if (attempt.Status != AttemptStatus.InProgress)
            throw new UserFriendlyException(AttemptErrorCodes.AlreadySubmitted, "Bài đã được nộp, không thể sửa đáp án.");

        if (DateTime.UtcNow >= attempt.ExpiresAtUtc)
        {
            // Thu bài ngay khi phát hiện hết hạn, trong transaction RIÊNG: ngoại lệ bên dưới sẽ rollback transaction của yêu cầu
            // hiện tại, và việc thu bài không được bị cuốn theo.
            using (var uow = UnitOfWorkManager.Begin(System.Transactions.TransactionScopeOption.RequiresNew))
            {
                await FinalizeAsync(attempt.Id);
                await uow.CompleteAsync();
            }

            throw new UserFriendlyException(AttemptErrorCodes.Expired, "Đã hết thời gian làm bài; bài đã được tự động nộp.");
        }
    }

    // ---------- nộp bài ----------

    /// <summary>Idempotent: nộp tay, nộp lặp và tự thu bài đồng thời chỉ tạo đúng một kết quả.</summary>
    public async Task<ExamAttempt> SubmitAsync(long userId, long attemptId)
    {
        var attempt = await GetOwnedAsync(userId, attemptId); // kiểm tra sở hữu
        if (attempt.Status == AttemptStatus.InProgress)
        {
            await FinalizeAsync(attemptId);
        }

        return await GetOwnedAsync(userId, attemptId);
    }

    /// <summary>
    /// Chuyển InProgress -> Submitted/AutoSubmitted bằng UPDATE có điều kiện rồi chấm trong cùng transaction.
    /// Người thua cuộc (affected = 0) không chấm lại. Quá hạn thì tính là AutoSubmitted, thời điểm nộp = hạn.
    /// </summary>
    public async Task FinalizeAsync(long attemptId)
    {
        var now = DateTime.UtcNow;
        var affected = await _attempts.GetAll()
            .Where(a => a.Id == attemptId && a.Status == AttemptStatus.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, a => a.ExpiresAtUtc <= now ? AttemptStatus.AutoSubmitted : AttemptStatus.Submitted)
                .SetProperty(a => a.SubmittedAtUtc, a => (DateTime?)(a.ExpiresAtUtc <= now ? a.ExpiresAtUtc : now)));
        if (affected == 0) return;

        await ScoreAsync(attemptId);
    }

    private async Task ScoreAsync(long attemptId)
    {
        var items = await _attemptQuestions.GetAll()
            .Include(x => x.Question)
            .Where(x => x.ExamAttemptId == attemptId)
            .ToListAsync();

        int correct = 0, wrong = 0, skipped = 0;
        foreach (var x in items)
        {
            if (x.SelectedOptionId == null)
            {
                x.IsCorrect = null;
                skipped++;
            }
            else if (x.SelectedOptionId == x.Question.CorrectOptionId)
            {
                x.IsCorrect = true;
                correct++;
            }
            else
            {
                x.IsCorrect = false;
                wrong++;
            }
        }

        var attempt = await _attempts.GetAll().FirstAsync(a => a.Id == attemptId);
        if (correct + wrong + skipped != attempt.TotalQuestions)
            throw new InvalidOperationException("Tổng đúng + sai + bỏ qua không bằng số câu của đề.");

        attempt.CorrectCount = correct;
        attempt.WrongCount = wrong;
        attempt.SkippedCount = skipped;
        attempt.ScorePercent = attempt.TotalQuestions == 0 ? 0 : Math.Round(100m * correct / attempt.TotalQuestions, 2);
        await CurrentUnitOfWork.SaveChangesAsync();
    }

    // ---------- tự thu bài (cho background worker) ----------

    public List<long> GetExpiredAttemptIds(int take)
    {
        var now = DateTime.UtcNow;
        return _attempts.GetAll().AsNoTracking()
            .Where(a => a.Status == AttemptStatus.InProgress && a.ExpiresAtUtc <= now)
            .OrderBy(a => a.ExpiresAtUtc)
            .Select(a => a.Id)
            .Take(take)
            .ToList();
    }
}
