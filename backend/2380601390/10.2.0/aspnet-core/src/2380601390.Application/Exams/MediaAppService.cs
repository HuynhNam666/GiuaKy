using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Web;
using _2380601390.Authorization;
using Abp.Domain.Entities;

namespace _2380601390.Exams;

public class MediaFileDto
{
    public byte[] Data { get; set; }
    public string ContentType { get; set; }
    public string Sha256 { get; set; }
}

public interface IMediaAppService : IApplicationService
{
    Task<MediaFileDto> GetAsync(long id);
}

/// <summary>Chỉ dùng nội bộ (controller phục vụ ảnh), không lộ ra Web API động.</summary>
[RemoteService(false)]
[AbpAuthorize(PermissionNames.Pages_Exams_Manage)]
public class MediaAppService : _2380601390AppServiceBase, IMediaAppService
{
    private readonly IRepository<MediaAsset, long> _media;

    public MediaAppService(IRepository<MediaAsset, long> media)
    {
        _media = media;
    }

    public async Task<MediaFileDto> GetAsync(long id)
    {
        var m = await _media.FirstOrDefaultAsync(id);
        if (m == null) throw new EntityNotFoundException(typeof(MediaAsset), id);
        return new MediaFileDto { Data = m.Data, ContentType = m.ContentType, Sha256 = m.Sha256 };
    }
}
