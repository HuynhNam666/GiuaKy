using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using _2380601390.Authorization;
using _2380601390.Controllers;
using _2380601390.Exams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace _2380601390.Web.Controllers;

/// <summary>Phục vụ ảnh nhúng của đề cho giảng viên xem trước (cache theo SHA-256 của ảnh).</summary>
[AbpMvcAuthorize(PermissionNames.Pages_Exams_Manage)]
public class MediaController : _2380601390ControllerBase
{
    private readonly IMediaAppService _media;

    public MediaController(IMediaAppService media)
    {
        _media = media;
    }

    [HttpGet]
    public async Task<ActionResult> Get(long id)
    {
        var m = await _media.GetAsync(id);
        var etag = "\"" + m.Sha256 + "\"";
        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) && inm == etag)
        {
            return StatusCode(304);
        }

        Response.Headers[HeaderNames.ETag] = etag;
        Response.Headers[HeaderNames.CacheControl] = "private, max-age=86400";
        return File(m.Data, m.ContentType);
    }
}
