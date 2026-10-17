using System;
using System.Linq;
using System.Threading.Tasks;
using _2380601390.Exams;
using _2380601390.Exams.Teacher;
using _2380601390.Exams.Teacher.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

public class ExamStatisticsAppService_Tests : _2380601390TestBase
{
    private readonly IExamImportAppService _import;
    private readonly IExamStatisticsAppService _stats;

    public ExamStatisticsAppService_Tests()
    {
        _import = Resolve<IExamImportAppService>();
        _stats = Resolve<IExamStatisticsAppService>();
    }

    private async Task<long> ImportAsync()
    {
        var r = await _import.ImportAsync(new ImportExamInput { FileName = "de.docx", Content = ExamImportTests.BuildFull100().ToArray() });
        r.Success.ShouldBeTrue();
        return r.ExamVersionId.Value;
    }

    [Fact]
    public async Task Version_without_attempts_has_zero_counts_and_no_average()
    {
        var id = await ImportAsync();
        var s = await _stats.GetAsync(id);
        s.FinishedCount.ShouldBe(0);
        s.InProgressCount.ShouldBe(0);
        s.AveragePercent.ShouldBeNull();
        s.TopWrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task Counts_average_and_top_wrong_use_only_submitted_attempts()
    {
        var id = await ImportAsync();
        var now = DateTime.UtcNow;

        await UsingDbContextAsync(async db =>
        {
            var qs = await db.Questions.Where(q => q.ExamVersionId == id).OrderBy(q => q.SourceNo).Take(3).ToListAsync();
            var qA = qs[0]; // sai ở cả 3 lượt đã nộp
            var qB = qs[1]; // sai 2 lượt, bỏ qua 1 lượt
            var qC = qs[2]; // luôn đúng

            ExamAttempt Make(long user, AttemptStatus status, decimal? score, DateTime expires)
            {
                var a = new ExamAttempt
                {
                    UserId = user,
                    ExamVersionId = id,
                    Status = status,
                    StartedAtUtc = now.AddMinutes(-80),
                    ExpiresAtUtc = expires,
                    TotalQuestions = 100,
                    ScorePercent = score
                };
                db.ExamAttempts.Add(a);
                return a;
            }

            void Row(ExamAttempt a, Question q, bool? correct, bool answered)
            {
                db.AttemptQuestions.Add(new AttemptQuestion
                {
                    ExamAttempt = a,
                    QuestionId = q.Id,
                    DisplayOrder = q.SourceNo,
                    SelectedOptionId = answered ? q.CorrectOptionId : null,
                    IsCorrect = correct
                });
            }

            var a1 = Make(11, AttemptStatus.Submitted, 60m, now.AddMinutes(-5));
            var a2 = Make(11, AttemptStatus.Submitted, 80m, now.AddMinutes(-4));      // cùng sinh viên làm lại
            var a3 = Make(12, AttemptStatus.AutoSubmitted, 40m, now.AddMinutes(-3));  // tự thu bài
            var running = Make(13, AttemptStatus.InProgress, null, now.AddMinutes(30));
            Make(14, AttemptStatus.InProgress, null, now.AddMinutes(-1));              // quá hạn, chờ worker: không tính là "đang làm"

            Row(a1, qA, false, true); Row(a1, qB, false, true); Row(a1, qC, true, true);
            Row(a2, qA, false, true); Row(a2, qB, null, false); Row(a2, qC, true, true);
            Row(a3, qA, false, true); Row(a3, qB, false, true); Row(a3, qC, true, true);
            Row(running, qA, false, true); // lượt đang làm không được vào Top 10
            await Task.CompletedTask;
        });

        var s = await _stats.GetAsync(id);

        s.FinishedCount.ShouldBe(3);
        s.DistinctStudents.ShouldBe(2);
        s.InProgressCount.ShouldBe(1);
        s.AutoSubmittedCount.ShouldBe(1);
        s.AveragePercent.ShouldBe(60m);

        s.TopWrong.Count.ShouldBe(2); // câu luôn đúng không xuất hiện
        s.TopWrong[0].Rank.ShouldBe(1);
        s.TopWrong[0].WrongCount.ShouldBe(3);
        s.TopWrong[0].QuestionNo.ShouldBe(101);
        s.TopWrong[0].WrongPercent.ShouldBe(100m);
        s.TopWrong[1].WrongCount.ShouldBe(2);
        s.TopWrong[1].SkippedCount.ShouldBe(1); // bỏ qua không tính là sai
        s.TopWrong[1].WrongPercent.ShouldBe(66.7m);
        s.TopWrong[1].PartNo.ShouldBe(5);
    }

    [Fact]
    public async Task Unknown_version_is_not_found()
    {
        await Should.ThrowAsync<Abp.Domain.Entities.EntityNotFoundException>(() => _stats.GetAsync(9999));
    }
}
