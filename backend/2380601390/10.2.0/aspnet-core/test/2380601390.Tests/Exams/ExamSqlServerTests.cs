using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Events.Bus;
using Abp.Events.Bus.Entities;
using _2380601390.EntityFrameworkCore;
using _2380601390.Exams;
using _2380601390.Exams.Import;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace _2380601390.Tests.Exams;

/// <summary>[Fact] chỉ chạy khi SQL Server localhost kết nối được; ngược lại được đánh dấu Skipped (không báo pass giả).</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Reachable = new Lazy<bool>(() =>
    {
        try
        {
            using var c = new SqlConnection(SqlServerFixture.MasterConnectionString + ";Connect Timeout=3");
            c.Open();
            return true;
        }
        catch
        {
            return false;
        }
    });

    public SqlServerFactAttribute()
    {
        if (!Reachable.Value) Skip = "SQL Server tại localhost không kết nối được.";
    }
}

public sealed class SqlServerFixture : IDisposable
{
    public const string MasterConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public string DbName { get; } = "ToeicTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    private bool _created;

    public string ConnectionString =>
        $"Server=localhost;Database={DbName};Trusted_Connection=True;TrustServerCertificate=True";

    public _2380601390DbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<_2380601390DbContext>().UseSqlServer(ConnectionString).Options;
        var db = new _2380601390DbContext(options)
        {
            EntityChangeEventHelper = NullEntityChangeEventHelper.Instance,
            EventBus = NullEventBus.Instance,
            SuppressAutoSetTenantId = true
        };
        if (!_created)
        {
            db.Database.Migrate(); // áp dụng đúng các migration của dự án lên DB thử nghiệm
            _created = true;
        }

        return db;
    }

    public void Dispose()
    {
        if (!_created) return;
        using var db = NewContext();
        db.Database.EnsureDeleted();
    }
}

/// <summary>Kiểm thử trên SQL Server thật: ràng buộc, transaction (all-or-nothing) và index lọc.</summary>
public class ExamSqlServerTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fx;

    public ExamSqlServerTests(SqlServerFixture fx)
    {
        _fx = fx;
    }

    /// <summary>Mô phỏng đúng việc Unit of Work của ABP làm: một transaction bao cả hai pha lưu.</summary>
    private static void Save(_2380601390DbContext db, ExamVersion version, ParsedExam parsed, bool commit = true)
    {
        using var tx = db.Database.BeginTransaction();
        db.ExamVersions.Add(version);
        ExamGraphBuilder.PersistAsync(version, parsed, () => db.SaveChangesAsync()).GetAwaiter().GetResult();
        if (commit) tx.Commit();
    }

    private static ParsedExam ParseFull(string examCode, int imageSeedBase)
    {
        var r = ExamDocxImporter.Parse(ExamImportTests.BuildFull100(imageSeedBase: imageSeedBase), new ExamImportOptions());
        r.Success.ShouldBeTrue(string.Join("\n", r.Errors));
        if (examCode != null) r.Exam.Id = examCode;
        return r.Exam;
    }

    [SqlServerFact]
    public void Full_exam_graph_is_saved_with_all_constraints_satisfied()
    {
        var parsed = ParseFull("SQL-FULL", 100);
        using (var db = _fx.NewContext())
        {
            var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('a', 64));
            version.Exam = new Exam { Code = parsed.Id, Title = parsed.Title };
            Save(db, version, parsed);
        }

        using (var db = _fx.NewContext())
        {
            var v = db.ExamVersions.Single(x => x.Exam.Code == "SQL-FULL");
            db.Questions.Count(q => q.ExamVersionId == v.Id).ShouldBe(100);
            db.QuestionOptions.Count(o => o.Question.ExamVersionId == v.Id).ShouldBe(400);
            db.Questions.Count(q => q.ExamVersionId == v.Id && q.CorrectOptionId != null).ShouldBe(100);
            db.Questions.Count(q => q.ExamVersionId == v.Id && q.CorrectOption.QuestionId != q.Id).ShouldBe(0);

            // Câu Part 6/7 không bao giờ tách khỏi nhóm; câu Part 5 không có nhóm.
            db.Questions.Count(q => q.ExamVersionId == v.Id && q.ExamPart.PartNo == 5 && q.ReadingGroupId != null).ShouldBe(0);
            db.Questions.Count(q => q.ExamVersionId == v.Id && q.ExamPart.PartNo != 5 && q.ReadingGroupId == null).ShouldBe(0);

            // Ảnh nằm đúng thứ tự khối trong tài liệu: văn bản, ảnh, văn bản.
            var doc = db.ReadingDocuments.First(d => d.Code == "D7-1a");
            db.ContentBlocks.Where(b => b.ReadingDocumentId == doc.Id).OrderBy(b => b.DisplayOrder).Select(b => b.BlockType)
                .ToList().ShouldBe(new[] { ContentBlockType.Text, ContentBlockType.Image, ContentBlockType.Text });
        }
    }

    [SqlServerFact]
    public void Failure_while_saving_rolls_back_everything_including_images()
    {
        var parsed = ParseFull("SQL-ROLLBACK", 200);
        int mediaBefore;
        using (var db = _fx.NewContext()) mediaBefore = db.MediaAssets.Count();

        // Làm hỏng câu CUỐI CÙNG (trùng số câu gốc với câu đầu): lỗi chỉ xuất hiện khi chạm ràng buộc UNIQUE của SQL Server.
        var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('b', 64));
        version.Exam = new Exam { Code = parsed.Id, Title = parsed.Title };
        var allQuestions = version.Parts.SelectMany(p => p.Questions).ToList();
        allQuestions.Last().SourceNo = allQuestions.First().SourceNo;

        using (var db = _fx.NewContext())
        {
            Should.Throw<DbUpdateException>(() => Save(db, version, parsed));
        }

        using (var db = _fx.NewContext())
        {
            db.Exams.Count(e => e.Code == "SQL-ROLLBACK").ShouldBe(0);
            db.MediaAssets.Count().ShouldBe(mediaBefore);
            db.Questions.Count(q => q.Code.StartsWith("P5-") && q.ExamVersion.Exam.Code == "SQL-ROLLBACK").ShouldBe(0);
        }
    }

    [SqlServerFact]
    public void Uncommitted_transaction_leaves_no_data_even_after_both_save_phases_succeeded()
    {
        var parsed = ParseFull("SQL-NOCOMMIT", 300);
        int mediaBefore;
        using (var db = _fx.NewContext()) mediaBefore = db.MediaAssets.Count();

        var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('9', 64));
        version.Exam = new Exam { Code = parsed.Id, Title = parsed.Title };
        using (var db = _fx.NewContext())
        {
            Save(db, version, parsed, commit: false); // cả hai pha đã chạy, nhưng không Commit
        }

        using (var db = _fx.NewContext())
        {
            db.Exams.Count(e => e.Code == "SQL-NOCOMMIT").ShouldBe(0);
            db.Questions.Count(q => q.ExamVersion.Exam.Code == "SQL-NOCOMMIT").ShouldBe(0);
            db.MediaAssets.Count().ShouldBe(mediaBefore);
        }
    }

    [SqlServerFact]
    public void Existing_images_are_reused_not_duplicated()
    {
        var parsed = ParseFull("SQL-MEDIA", 400);
        var first = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('c', 64));
        first.Exam = new Exam { Code = "SQL-MEDIA", Title = "m" };
        using (var db = _fx.NewContext())
        {
            Save(db, first, parsed);
        }

        Dictionary<string, long> existing;
        using (var db = _fx.NewContext())
        {
            var shas = parsed.Parts.SelectMany(p => p.Groups).SelectMany(g => g.Documents).SelectMany(d => d.Blocks)
                .Where(b => b.Image != null).Select(b => b.Image.Sha256).ToList();
            existing = db.MediaAssets.Where(m => shas.Contains(m.Sha256)).Select(m => new { m.Id, m.Sha256 }).ToList()
                .ToDictionary(x => x.Sha256, x => x.Id);
        }

        existing.Count.ShouldBe(25);
        int before;
        using (var db = _fx.NewContext()) before = db.MediaAssets.Count();

        var second = ExamGraphBuilder.BuildVersion(parsed, 2, existing, "x.docx", new string('d', 64));
        using (var db = _fx.NewContext())
        {
            second.ExamId = db.Exams.Single(e => e.Code == "SQL-MEDIA").Id;
            Save(db, second, parsed);
            db.MediaAssets.Count().ShouldBe(before);
        }
    }

    [SqlServerFact]
    public void Content_block_check_constraint_rejects_empty_block()
    {
        var parsed = ParseFull("SQL-CHECK", 500);
        var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('e', 64));
        version.Exam = new Exam { Code = "SQL-CHECK", Title = "c" };
        version.Parts.SelectMany(p => p.Groups).SelectMany(g => g.Documents).SelectMany(d => d.Blocks)
            .First(b => b.BlockType == ContentBlockType.Text).Text = null;

        using var db = _fx.NewContext();
        Should.Throw<DbUpdateException>(() => Save(db, version, parsed));
    }

    [SqlServerFact]
    public void Cloning_a_version_saves_a_deep_independent_copy_with_correct_answers()
    {
        var parsed = ParseFull("SQL-CLONE", 700);
        long srcId;
        var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('1', 64));
        version.Exam = new Exam { Code = "SQL-CLONE", Title = "clone" };
        using (var db = _fx.NewContext())
        {
            Save(db, version, parsed);
            srcId = version.Id;
        }

        long copyId;
        using (var db = _fx.NewContext())
        {
            var src = db.ExamVersions
                .Include(v => v.Exam)
                .Include(v => v.Parts).ThenInclude(p => p.Groups).ThenInclude(g => g.Documents).ThenInclude(d => d.Blocks)
                .Include(v => v.Parts).ThenInclude(p => p.Questions).ThenInclude(q => q.Options)
                .AsSplitQuery()
                .Single(v => v.Id == srcId);

            var clone = ExamVersionCloner.Clone(src, 2);
            using var tx = db.Database.BeginTransaction();
            db.ExamVersions.Add(clone.Version);
            ExamVersionCloner.PersistAsync(clone, () => db.SaveChangesAsync()).GetAwaiter().GetResult();
            tx.Commit();
            copyId = clone.Version.Id;
        }

        using (var db = _fx.NewContext())
        {
            db.Questions.Count(q => q.ExamVersionId == copyId).ShouldBe(100);
            db.QuestionOptions.Count(o => o.Question.ExamVersionId == copyId).ShouldBe(400);
            db.Questions.Count(q => q.ExamVersionId == copyId && q.CorrectOptionId != null).ShouldBe(100);
            db.Questions.Count(q => q.ExamVersionId == copyId && q.CorrectOption.QuestionId != q.Id).ShouldBe(0);
            // Đáp án đúng của bản sao trùng mã với bản gốc cho từng câu.
            var diff = (from c in db.Questions
                        join o in db.Questions on c.Code equals o.Code
                        where c.ExamVersionId == copyId && o.ExamVersionId == srcId && c.CorrectOption.Code != o.CorrectOption.Code
                        select c.Id).Count();
            diff.ShouldBe(0);
            // Ảnh dùng chung, không nhân đôi.
            db.ContentBlocks.Where(b => b.ReadingDocument.ReadingGroup.ExamPart.ExamVersionId == copyId && b.MediaAssetId != null)
                .Select(b => b.MediaAssetId).Distinct().Count().ShouldBe(25);
            // Hai bản không dùng chung bản ghi câu hỏi/lựa chọn.
            db.Questions.Where(q => q.ExamVersionId == copyId).Select(q => q.Id)
                .Intersect(db.Questions.Where(q => q.ExamVersionId == srcId).Select(q => q.Id)).Count().ShouldBe(0);
        }
    }

    [SqlServerFact]
    public void Only_one_in_progress_attempt_per_user_and_exam_version()
    {
        var parsed = ParseFull("SQL-ATTEMPT", 600);
        long versionId;
        var version = ExamGraphBuilder.BuildVersion(parsed, 1, new Dictionary<string, long>(), "x.docx", new string('f', 64));
        version.Exam = new Exam { Code = "SQL-ATTEMPT", Title = "a" };
        using (var db = _fx.NewContext())
        {
            Save(db, version, parsed);
            versionId = version.Id;
        }

        ExamAttempt Attempt(AttemptStatus s) => new ExamAttempt
        {
            UserId = 99,
            ExamVersionId = versionId,
            Status = s,
            StartedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(75),
            TotalQuestions = 100
        };

        using (var db = _fx.NewContext())
        {
            db.ExamAttempts.Add(Attempt(AttemptStatus.Submitted));
            db.ExamAttempts.Add(Attempt(AttemptStatus.Submitted));
            db.ExamAttempts.Add(Attempt(AttemptStatus.InProgress));
            db.SaveChanges(); // nhiều lượt đã nộp + một lượt đang làm: hợp lệ
        }

        using (var db = _fx.NewContext())
        {
            db.ExamAttempts.Add(Attempt(AttemptStatus.InProgress));
            Should.Throw<DbUpdateException>(() => db.SaveChanges());
        }
    }
}
