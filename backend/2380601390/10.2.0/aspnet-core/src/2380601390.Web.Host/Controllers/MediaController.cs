using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using _2380601390.Controllers;
using _2380601390.Exams.Student;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace _2380601390.Web.Host.Controllers
{
    /// <summary>GET /api/media/{sha256}: ảnh của đề đã xuất bản cho ứng dụng sinh viên.</summary>
    [Route("api/media")]
    public class MediaController : _2380601390ControllerBase
    {
        private readonly IStudentMediaAppService _media;

        public MediaController(IStudentMediaAppService media)
        {
            _media = media;
        }

        [HttpGet("{sha256}")]
        [AllowAnonymous] // ảnh được bảo vệ bằng SHA-256 khó đoán và chỉ phục vụ ảnh của đề không phải Nháp
        public async Task<ActionResult> Get(string sha256)
        {
            var m = await _media.GetPublicAsync(sha256);
            var etag = "\"" + m.Sha256 + "\"";
            if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) && inm == etag)
            {
                return StatusCode(304);
            }

            Response.Headers[HeaderNames.ETag] = etag;
            Response.Headers[HeaderNames.CacheControl] = "public, max-age=86400";
            return File(m.Data, m.ContentType);
        }
    }
}
