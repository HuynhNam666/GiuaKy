using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.Domain.Services;

namespace _2380601390.Exams.Import;

/// <summary>
/// Lưu ParsedExam đã qua kiểm tra vào CSDL. Phải chạy trong một Unit of Work (AppService của ABP mặc định có);
/// toàn bộ đồ thị được ghi bằng một SaveChanges nên hoặc lưu hết, hoặc không lưu gì.
/// Ảnh nằm trong bảng MediaAsset (cùng transaction) nên không có tệp tạm cần dọn.
/// </summary>
public class ExamImportManager : DomainService
{
    private readonly IRepository<Exam, long> _exams;
    private readonly IRepository<ExamVersion, long> _versions;
    private readonly IRepository<MediaAsset, long> _media;

    public ExamImportManager(
        IRepository<Exam, long> exams,
        IRepository<ExamVersion, long> versions,
        IRepository<MediaAsset, long> media)
    {
        _exams = exams;
        _versions = versions;
        _media = media;
    }

    /// <summary>Tạo phiên bản Nháp mới. Nếu mã đề (EXAM id) đã tồn tại thì thêm phiên bản kế tiếp cho đề đó.</summary>
    public async Task<ExamVersion> SaveAsDraftAsync(ParsedExam parsed, string sourceFileName, string sourceFileSha256)
    {
        var exam = await _exams.FirstOrDefaultAsync(e => e.Code == parsed.Id);

        var versionNo = 1;
        if (exam != null)
        {
            // Tính cả bản đã xóa mềm để số phiên bản không bao giờ bị dùng lại (có unique index).
            using (CurrentUnitOfWork.DisableFilter(Abp.Domain.Uow.AbpDataFilters.SoftDelete))
            {
                var nos = _versions.GetAll().Where(v => v.ExamId == exam.Id).Select(v => v.VersionNo).ToList();
                versionNo = (nos.Count == 0 ? 0 : nos.Max()) + 1;
            }
        }

        var shas = parsed.Parts
            .SelectMany(p => p.Groups)
            .SelectMany(g => g.Documents)
            .SelectMany(d => d.Blocks)
            .Where(b => b.Type == ContentBlockType.Image)
            .Select(b => b.Image.Sha256)
            .Distinct()
            .ToList();

        // Chỉ lấy Id + hash, không tải dữ liệu ảnh.
        var existing = shas.Count == 0
            ? new Dictionary<string, long>()
            : _media.GetAll().Where(m => shas.Contains(m.Sha256)).Select(m => new { m.Id, m.Sha256 }).ToList()
                .ToDictionary(x => x.Sha256, x => x.Id);

        var version = ExamGraphBuilder.BuildVersion(parsed, versionNo, existing, sourceFileName, sourceFileSha256);

        if (exam == null)
        {
            exam = new Exam { Code = parsed.Id, Title = parsed.Title };
            exam.Versions.Add(version);
            version.Exam = exam;
            await _exams.InsertAsync(exam);
        }
        else
        {
            exam.Title = parsed.Title;
            version.ExamId = exam.Id;
            await _versions.InsertAsync(version);
        }

        await ExamGraphBuilder.PersistAsync(version, parsed, () => CurrentUnitOfWork.SaveChangesAsync());
        return version;
    }
}
