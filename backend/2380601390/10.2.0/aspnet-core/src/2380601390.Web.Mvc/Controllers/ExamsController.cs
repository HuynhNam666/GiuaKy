using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp.AspNetCore.Mvc.Authorization;
using _2380601390.Authorization;
using _2380601390.Controllers;
using _2380601390.Exams.Teacher;
using _2380601390.Exams.Teacher.Dto;
using _2380601390.Web.Models.Exams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace _2380601390.Web.Controllers;

[AbpMvcAuthorize(PermissionNames.Pages_Exams_Manage)]
public class ExamsController : _2380601390ControllerBase
{
    private readonly IExamAppService _examAppService;
    private readonly IExamImportAppService _importAppService;
    private readonly IExamStatisticsAppService _statisticsAppService;

    public ExamsController(IExamAppService examAppService, IExamImportAppService importAppService, IExamStatisticsAppService statisticsAppService)
    {
        _examAppService = examAppService;
        _importAppService = importAppService;
        _statisticsAppService = statisticsAppService;
    }

    /// <param name="id">Id phiên bản đề; mặc định: phiên bản đang xuất bản mới nhất, nếu không có thì phiên bản mới nhất.</param>
    [AbpMvcAuthorize(PermissionNames.Pages_Exams_Statistics)]
    public async Task<ActionResult> Statistics(long? id)
    {
        var versions = await _examAppService.GetVersionsAsync();
        var model = new ExamStatisticsViewModel { Versions = versions };
        var chosen = id.HasValue
            ? versions.FirstOrDefault(v => v.VersionId == id.Value)
            : versions.Where(v => v.Status == _2380601390.Exams.ExamVersionStatus.Published).OrderByDescending(v => v.CreationTime).FirstOrDefault()
              ?? versions.OrderByDescending(v => v.CreationTime).FirstOrDefault();
        if (chosen != null) model.Stats = await _statisticsAppService.GetAsync(chosen.VersionId);
        return View(model);
    }

    public async Task<ActionResult> Index()
    {
        var model = new ExamListViewModel
        {
            Versions = await _examAppService.GetVersionsAsync(),
            SuccessMessage = TempData["ImportSuccess"] as string
        };
        return View(model);
    }

    /// <param name="id">Id phiên bản đề.</param>
    /// <param name="sel">"p5" = câu độc lập Part 5; "g{id}" = một nhóm bài đọc. Mặc định: Part 5.</param>
    public async Task<ActionResult> Preview(long id, string sel = null)
    {
        var preview = await _examAppService.GetPreviewAsync(id);
        var model = new ExamPreviewViewModel { Preview = preview, SelectedKey = sel ?? "p5" };
        return View(model);
    }

    [HttpPost]
    [RequestSizeLimit(ExamImportAppService.MaxFileBytes + 1024 * 1024)]
    public async Task<ActionResult> Upload(IFormFile file)
    {
        var model = new ExamListViewModel();
        if (file == null || file.Length == 0)
        {
            model.Errors.Add(new ImportErrorDto { Code = "E_FILE_INVALID", Message = "Chưa chọn tệp .docx." });
        }
        else
        {
            model.UploadedFileName = file.FileName;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var result = await _importAppService.ImportAsync(new ImportExamInput
            {
                FileName = file.FileName,
                Content = ms.ToArray()
            });

            if (result.Success)
            {
                TempData["ImportSuccess"] =
                    $"Đã tạo đề \"{result.Title}\" (phiên bản {result.VersionNo}, trạng thái Nháp): " +
                    $"{result.QuestionCount} câu, {result.ImageCount} ảnh.";
                return RedirectToAction(nameof(Index));
            }

            model.Errors = result.Errors;
        }

        model.Versions = await _examAppService.GetVersionsAsync();
        return View(nameof(Index), model);
    }
}
