using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Abp.UI;
using _2380601390.Exams.Import;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.Exams;

/// <summary>
/// Nghiệp vụ trên phiên bản đề đã lưu: nạp, kiểm tra lại, sửa (chỉ bản Nháp), xuất bản, dừng, tạo bản sửa, xóa nháp.
/// Quy tắc cốt lõi: phiên bản đã xuất bản là BẤT BIẾN; muốn sửa phải tạo bản Nháp mới.
/// </summary>
public class ExamVersionManager : DomainService
{
    private static readonly string[] OptionCodes = { "A", "B", "C", "D" };

    private readonly IRepository<ExamVersion, long> _versions;
    private readonly IRepository<ExamPart, long> _parts;
    private readonly IRepository<ReadingGroup, long> _groups;
    private readonly IRepository<ContentBlock, long> _blocks;
    private readonly IRepository<Question, long> _questions;

    public ExamVersionManager(
        IRepository<ExamVersion, long> versions,
        IRepository<ExamPart, long> parts,
        IRepository<ReadingGroup, long> groups,
        IRepository<ContentBlock, long> blocks,
        IRepository<Question, long> questions)
    {
        _versions = versions;
        _parts = parts;
        _groups = groups;
        _blocks = blocks;
        _questions = questions;
    }

    // ---------- nạp & kiểm tra ----------

    public IQueryable<ExamVersion> FullQuery()
    {
        return _versions.GetAll()
            .Include(v => v.Exam)
            .Include(v => v.Parts).ThenInclude(p => p.Groups).ThenInclude(g => g.Documents).ThenInclude(d => d.Blocks)
            .Include(v => v.Parts).ThenInclude(p => p.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery();
    }

    public async Task<ExamVersion> GetFullAsync(long versionId)
    {
        var v = await FullQuery().FirstOrDefaultAsync(x => x.Id == versionId);
        if (v == null) throw new EntityNotFoundException(typeof(ExamVersion), versionId);
        return v;
    }

    /// <summary>Chạy lại đúng bộ kiểm tra của import trên dữ liệu đã lưu.</summary>
    public List<ImportError> Validate(ExamVersion version)
    {
        return ExamValidator.Validate(ExamVersionMapper.ToParsed(version), ExamRules.ValidationOptions());
    }

    public async Task<List<ImportError>> ValidateAsync(long versionId)
    {
        return Validate(await GetFullAsync(versionId));
    }

    private static void EnsureDraft(ExamVersion v)
    {
        if (v.Status != ExamVersionStatus.Draft)
        {
            throw new UserFriendlyException("Chỉ sửa được phiên bản Nháp. Phiên bản đã xuất bản là bất biến — hãy dùng \"Tạo bản sửa\" để sửa.");
        }
    }

    // ---------- sửa (chỉ bản Nháp) ----------

    public async Task<long> UpdateQuestionAsync(long questionId, string stem, string explanation, bool shuffleOptions,
        string correctCode, IDictionary<string, string> optionTexts)
    {
        var q = await _questions.GetAll()
            .Include(x => x.Options).Include(x => x.ExamPart).Include(x => x.ExamVersion)
            .FirstOrDefaultAsync(x => x.Id == questionId);
        if (q == null) throw new EntityNotFoundException(typeof(Question), questionId);
        EnsureDraft(q.ExamVersion);

        if (optionTexts == null || optionTexts.Count != 4 || OptionCodes.Any(c => !optionTexts.ContainsKey(c)))
            throw new UserFriendlyException("Câu hỏi phải có đúng 4 lựa chọn A, B, C, D.");
        foreach (var c in OptionCodes)
        {
            if (string.IsNullOrWhiteSpace(optionTexts[c]))
                throw new UserFriendlyException("Lựa chọn " + c + " không được để trống.");
        }

        if (!OptionCodes.Contains(correctCode))
            throw new UserFriendlyException("Đáp án đúng phải là A, B, C hoặc D.");
        if (string.IsNullOrWhiteSpace(explanation))
            throw new UserFriendlyException("Gợi ý/giải thích không được để trống.");
        if (q.ExamPart.PartNo != 6 && string.IsNullOrWhiteSpace(stem))
            throw new UserFriendlyException("Nội dung câu hỏi không được để trống (chỉ Part 6 được để trống).");

        q.StemText = string.IsNullOrWhiteSpace(stem) ? null : stem.Trim();
        q.Explanation = explanation.Trim();
        q.ShuffleOptions = shuffleOptions;
        foreach (var o in q.Options) o.Text = optionTexts[o.Code].Trim();
        q.CorrectOptionId = q.Options.Single(o => o.Code == correctCode).Id;
        return q.ExamVersionId;
    }

    public async Task<long> UpdateGroupAsync(long groupId, bool shuffleQuestions)
    {
        var g = await _groups.GetAll().Include(x => x.ExamPart).ThenInclude(p => p.ExamVersion)
            .FirstOrDefaultAsync(x => x.Id == groupId);
        if (g == null) throw new EntityNotFoundException(typeof(ReadingGroup), groupId);
        EnsureDraft(g.ExamPart.ExamVersion);
        if (g.ExamPart.PartNo == 6 && shuffleQuestions)
            throw new UserFriendlyException("Part 6 không cho hoán vị câu trong nhóm (thứ tự câu gắn với chỗ trống).");
        g.ShuffleQuestions = shuffleQuestions;
        return g.ExamPart.ExamVersionId;
    }

    public async Task<long> UpdatePartAsync(long partId, bool shuffleItems)
    {
        var p = await _parts.GetAll().Include(x => x.ExamVersion).FirstOrDefaultAsync(x => x.Id == partId);
        if (p == null) throw new EntityNotFoundException(typeof(ExamPart), partId);
        EnsureDraft(p.ExamVersion);
        p.ShuffleItems = shuffleItems;
        return p.ExamVersionId;
    }

    public async Task<long> UpdateBlockTextAsync(long blockId, string text)
    {
        var b = await _blocks.GetAll()
            .Include(x => x.ReadingDocument).ThenInclude(d => d.ReadingGroup).ThenInclude(g => g.ExamPart).ThenInclude(p => p.ExamVersion)
            .FirstOrDefaultAsync(x => x.Id == blockId);
        if (b == null) throw new EntityNotFoundException(typeof(ContentBlock), blockId);
        var version = b.ReadingDocument.ReadingGroup.ExamPart.ExamVersion;
        EnsureDraft(version);
        if (b.BlockType != ContentBlockType.Text) throw new UserFriendlyException("Chỉ sửa được khối văn bản (không sửa ảnh).");
        if (string.IsNullOrWhiteSpace(text)) throw new UserFriendlyException("Nội dung văn bản không được để trống.");
        b.Text = text.Trim();
        return version.Id;
    }

    public async Task<long> UpdateSettingsAsync(long versionId, string title, int durationMinutes)
    {
        var v = await _versions.GetAll().Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == versionId);
        if (v == null) throw new EntityNotFoundException(typeof(ExamVersion), versionId);
        EnsureDraft(v);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 256) throw new UserFriendlyException("Tên đề không được rỗng và tối đa 256 ký tự.");
        if (durationMinutes < 1 || durationMinutes > ExamRules.MaxDurationMinutes)
            throw new UserFriendlyException("Thời gian làm bài phải từ 1 đến " + ExamRules.MaxDurationMinutes + " phút.");
        v.Exam.Title = title.Trim();
        v.DurationMinutes = durationMinutes;
        return v.Id;
    }

    // ---------- xuất bản / dừng / bản sửa / xóa ----------

    /// <summary>
    /// Xuất bản bản Nháp sau khi kiểm tra lại toàn bộ. Có lỗi thì không đổi trạng thái và trả về danh sách lỗi.
    /// Mỗi đề chỉ có một bản đang xuất bản: bản xuất bản trước đó chuyển thành Lưu trữ
    /// (các lượt đang làm trên bản cũ vẫn hoàn thành bình thường).
    /// </summary>
    public async Task<List<ImportError>> PublishAsync(long versionId)
    {
        var v = await GetFullAsync(versionId);
        if (v.Status != ExamVersionStatus.Draft)
            throw new UserFriendlyException("Chỉ xuất bản được phiên bản Nháp.");

        var errors = Validate(v);
        if (errors.Count > 0) return errors;

        var previous = await _versions.GetAllListAsync(x => x.ExamId == v.ExamId && x.Status == ExamVersionStatus.Published && x.Id != v.Id);
        foreach (var p in previous) p.Status = ExamVersionStatus.Archived;

        v.Status = ExamVersionStatus.Published;
        v.PublishedAtUtc = DateTime.UtcNow;
        return errors;
    }

    /// <summary>Dừng nhận thi: Đã xuất bản -> Lưu trữ.</summary>
    public async Task UnpublishAsync(long versionId)
    {
        var v = await _versions.FirstOrDefaultAsync(versionId);
        if (v == null) throw new EntityNotFoundException(typeof(ExamVersion), versionId);
        if (v.Status != ExamVersionStatus.Published) throw new UserFriendlyException("Phiên bản này chưa được xuất bản.");
        v.Status = ExamVersionStatus.Archived;
    }

    /// <summary>Tạo bản Nháp mới (bản sao sâu) từ một phiên bản bất kỳ. Mỗi đề chỉ được có một bản Nháp.</summary>
    public async Task<ExamVersion> CreateDraftFromAsync(long sourceVersionId)
    {
        var src = await GetFullAsync(sourceVersionId);
        if (await _versions.CountAsync(x => x.ExamId == src.ExamId && x.Status == ExamVersionStatus.Draft) > 0)
            throw new UserFriendlyException("Đề này đã có một bản Nháp. Hãy sửa hoặc xóa bản Nháp đó trước.");

        int next;
        using (CurrentUnitOfWork.DisableFilter(Abp.Domain.Uow.AbpDataFilters.SoftDelete))
        {
            next = _versions.GetAll().Where(x => x.ExamId == src.ExamId).Select(x => x.VersionNo).ToList().Max() + 1;
        }

        var clone = ExamVersionCloner.Clone(src, next);
        await _versions.InsertAsync(clone.Version);
        await ExamVersionCloner.PersistAsync(clone, () => CurrentUnitOfWork.SaveChangesAsync());
        return clone.Version;
    }

    /// <summary>Xóa (mềm) bản Nháp. Bản đã xuất bản/lưu trữ có thể gắn với lượt thi nên không được xóa.</summary>
    public async Task DeleteDraftAsync(long versionId)
    {
        var v = await _versions.FirstOrDefaultAsync(versionId);
        if (v == null) throw new EntityNotFoundException(typeof(ExamVersion), versionId);
        if (v.Status != ExamVersionStatus.Draft) throw new UserFriendlyException("Chỉ xóa được phiên bản Nháp.");
        await _versions.DeleteAsync(v);
    }
}
