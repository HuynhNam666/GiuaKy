using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.Web;
using Microsoft.EntityFrameworkCore;

namespace _2380601390.Exams.Student;

public interface IStudentMediaAppService : IApplicationService
{
    Task<MediaFileDto> GetPublicAsync(string sha256);
}

/// <summary>
/// Ảnh của đề cho sinh viên. Thẻ &lt;img&gt; không gửi được header Authorization nên ảnh được truy cập theo SHA-256 của nội dung
/// (khó đoán), và chỉ với ảnh thuộc phiên bản KHÔNG còn là Nháp. Không lộ ra Web API động; dùng qua controller của Web.Host.
/// </summary>
[RemoteService(false)]
[AbpAllowAnonymous]
public class StudentMediaAppService : _2380601390AppServiceBase, IStudentMediaAppService
{
    private readonly IRepository<MediaAsset, long> _media;
    private readonly IRepository<ContentBlock, long> _blocks;

    public StudentMediaAppService(IRepository<MediaAsset, long> media, IRepository<ContentBlock, long> blocks)
    {
        _media = media;
        _blocks = blocks;
    }

    public async Task<MediaFileDto> GetPublicAsync(string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64) throw new EntityNotFoundException(typeof(MediaAsset), sha256);

        var allowed = await _blocks.GetAll().AnyAsync(b => b.MediaAsset.Sha256 == sha256
            && b.ReadingDocument.ReadingGroup.ExamPart.ExamVersion.Status != ExamVersionStatus.Draft);
        var m = allowed ? await _media.GetAll().FirstOrDefaultAsync(x => x.Sha256 == sha256) : null;
        if (m == null) throw new EntityNotFoundException(typeof(MediaAsset), sha256);
        return new MediaFileDto { Data = m.Data, ContentType = m.ContentType, Sha256 = m.Sha256 };
    }
}
