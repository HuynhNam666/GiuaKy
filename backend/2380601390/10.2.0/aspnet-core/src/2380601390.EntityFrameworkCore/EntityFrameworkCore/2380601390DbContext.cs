using Abp.Zero.EntityFrameworkCore;
using _2380601390.Authorization.Roles;
using _2380601390.Authorization.Users;
using _2380601390.Exams;
using _2380601390.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.EntityFrameworkCore;

public class _2380601390DbContext : AbpZeroDbContext<Tenant, Role, User, _2380601390DbContext>
{
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamVersion> ExamVersions { get; set; }
    public DbSet<ExamPart> ExamParts { get; set; }
    public DbSet<ReadingGroup> ReadingGroups { get; set; }
    public DbSet<ReadingDocument> ReadingDocuments { get; set; }
    public DbSet<ContentBlock> ContentBlocks { get; set; }
    public DbSet<MediaAsset> MediaAssets { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<ExamAttempt> ExamAttempts { get; set; }
    public DbSet<AttemptGroup> AttemptGroups { get; set; }
    public DbSet<AttemptQuestion> AttemptQuestions { get; set; }
    public DbSet<AttemptOption> AttemptOptions { get; set; }

    public _2380601390DbContext(DbContextOptions<_2380601390DbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Exam>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<ExamVersion>(e =>
        {
            e.HasOne(x => x.Exam).WithMany(x => x.Versions).HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ExamId, x.VersionNo }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<ExamPart>(e =>
        {
            e.HasOne(x => x.ExamVersion).WithMany(x => x.Parts).HasForeignKey(x => x.ExamVersionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ExamVersionId, x.PartNo }).IsUnique();
        });

        b.Entity<ReadingGroup>(e =>
        {
            e.HasOne(x => x.ExamPart).WithMany(x => x.Groups).HasForeignKey(x => x.ExamPartId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ExamPartId, x.Code }).IsUnique();
        });

        b.Entity<ReadingDocument>(e =>
        {
            e.HasOne(x => x.ReadingGroup).WithMany(x => x.Documents).HasForeignKey(x => x.ReadingGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ReadingGroupId, x.DisplayOrder }).IsUnique();
        });

        b.Entity<MediaAsset>(e =>
        {
            e.HasIndex(x => x.Sha256).IsUnique();
        });

        b.Entity<ContentBlock>(e =>
        {
            e.HasOne(x => x.ReadingDocument).WithMany(x => x.Blocks).HasForeignKey(x => x.ReadingDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MediaAsset).WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ReadingDocumentId, x.DisplayOrder }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint(
                "CK_ContentBlock_TextOrImage",
                "([BlockType] = 0 AND [Text] IS NOT NULL AND [MediaAssetId] IS NULL) OR ([BlockType] = 1 AND [MediaAssetId] IS NOT NULL)"));
        });

        b.Entity<Question>(e =>
        {
            e.HasOne(x => x.ExamVersion).WithMany().HasForeignKey(x => x.ExamVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ExamPart).WithMany(x => x.Questions).HasForeignKey(x => x.ExamPartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReadingGroup).WithMany(x => x.Questions).HasForeignKey(x => x.ReadingGroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CorrectOption).WithMany().HasForeignKey(x => x.CorrectOptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExamVersionId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.ExamVersionId, x.SourceNo }).IsUnique();
            e.HasIndex(x => x.ReadingGroupId);
        });

        b.Entity<QuestionOption>(e =>
        {
            e.HasOne(x => x.Question).WithMany(x => x.Options).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.QuestionId, x.Code }).IsUnique();
        });

        b.Entity<ExamAttempt>(e =>
        {
            e.HasOne(x => x.ExamVersion).WithMany().HasForeignKey(x => x.ExamVersionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ScorePercent).HasPrecision(5, 2);
            e.HasIndex(x => new { x.ExamVersionId, x.Status });
            e.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
            e.HasIndex(x => new { x.UserId, x.ExamVersionId })
                .IsUnique()
                .HasFilter("[Status] = 0")
                .HasDatabaseName("UX_ExamAttempt_OneInProgress");
        });

        b.Entity<AttemptGroup>(e =>
        {
            e.HasOne(x => x.ExamAttempt).WithMany(x => x.Groups).HasForeignKey(x => x.ExamAttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReadingGroup).WithMany().HasForeignKey(x => x.ReadingGroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExamAttemptId, x.ReadingGroupId }).IsUnique();
            e.HasIndex(x => new { x.ExamAttemptId, x.DisplayOrder }).IsUnique();
        });

        b.Entity<AttemptQuestion>(e =>
        {
            e.HasOne(x => x.ExamAttempt).WithMany(x => x.Questions).HasForeignKey(x => x.ExamAttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SelectedOption).WithMany().HasForeignKey(x => x.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ExamAttemptId, x.QuestionId }).IsUnique();
            e.HasIndex(x => new { x.ExamAttemptId, x.DisplayOrder }).IsUnique();
            e.HasIndex(x => new { x.QuestionId, x.IsCorrect });
        });

        b.Entity<AttemptOption>(e =>
        {
            e.HasOne(x => x.AttemptQuestion).WithMany(x => x.Options).HasForeignKey(x => x.AttemptQuestionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.QuestionOption).WithMany().HasForeignKey(x => x.QuestionOptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AttemptQuestionId, x.QuestionOptionId }).IsUnique();
            e.HasIndex(x => new { x.AttemptQuestionId, x.DisplayOrder }).IsUnique();
        });
    }
}
