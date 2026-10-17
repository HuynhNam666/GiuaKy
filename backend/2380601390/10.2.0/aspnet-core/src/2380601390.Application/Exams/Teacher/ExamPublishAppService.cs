using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using _2380601390.Authorization;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Exams.Teacher;

public interface IExamPublishAppService : Abp.Application.Services.IApplicationService
{
    Task<List<ImportErrorDto>> ValidateAsync(VersionIdInput input);
    Task<PublishResultDto> PublishAsync(VersionIdInput input);
    Task UnpublishAsync(VersionIdInput input);
    Task<long> CreateDraftAsync(VersionIdInput input);
    Task DeleteDraftAsync(VersionIdInput input);
}

/// <summary>
/// Vòng đời phiên bản: Nháp -> Đã xuất bản -> Lưu trữ. Chỉ phiên bản "Đã xuất bản" sinh viên mới thấy và được bắt đầu thi.
/// "Kích hoạt ca thi" tối thiểu = Xuất bản (không có lịch thi/giờ mở-đóng).
/// </summary>
[AbpAuthorize(PermissionNames.Pages_Exams_Manage)]
public class ExamPublishAppService : _2380601390AppServiceBase, IExamPublishAppService
{
    private readonly ExamVersionManager _manager;

    public ExamPublishAppService(ExamVersionManager manager)
    {
        _manager = manager;
    }

    public async Task<List<ImportErrorDto>> ValidateAsync(VersionIdInput input)
    {
        return (await _manager.ValidateAsync(input.VersionId)).Select(e => e.ToDto()).ToList();
    }

    [AbpAuthorize(PermissionNames.Pages_Exams_Publish)]
    public async Task<PublishResultDto> PublishAsync(VersionIdInput input)
    {
        var errors = await _manager.PublishAsync(input.VersionId);
        return new PublishResultDto { Success = errors.Count == 0, Errors = errors.Select(e => e.ToDto()).ToList() };
    }

    [AbpAuthorize(PermissionNames.Pages_Exams_Publish)]
    public Task UnpublishAsync(VersionIdInput input) => _manager.UnpublishAsync(input.VersionId);

    /// <summary>Tạo bản Nháp mới từ một phiên bản; trả về Id bản Nháp.</summary>
    public async Task<long> CreateDraftAsync(VersionIdInput input)
    {
        var draft = await _manager.CreateDraftFromAsync(input.VersionId);
        return draft.Id;
    }

    public Task DeleteDraftAsync(VersionIdInput input) => _manager.DeleteDraftAsync(input.VersionId);
}
