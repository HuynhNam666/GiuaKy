using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
using _2380601390.Authorization;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Exams.Teacher;

public interface IExamEditAppService : Abp.Application.Services.IApplicationService
{
    Task<EditResultDto> UpdateQuestionAsync(UpdateQuestionInput input);
    Task<EditResultDto> UpdateGroupAsync(UpdateGroupInput input);
    Task<EditResultDto> UpdatePartAsync(UpdatePartInput input);
    Task<EditResultDto> UpdateBlockTextAsync(UpdateBlockTextInput input);
    Task<EditResultDto> UpdateSettingsAsync(UpdateSettingsInput input);
}

/// <summary>
/// Sửa nội dung đề. Chỉ sửa được bản Nháp (bản đã xuất bản bất biến). Dữ liệu sai quy tắc bị từ chối ngay;
/// sau mỗi lần sửa thành công, toàn bộ phiên bản được kiểm tra lại và các vấn đề (nếu có) trả về cho giao diện.
/// </summary>
[AbpAuthorize(PermissionNames.Pages_Exams_Manage)]
public class ExamEditAppService : _2380601390AppServiceBase, IExamEditAppService
{
    private readonly ExamVersionManager _manager;

    public ExamEditAppService(ExamVersionManager manager)
    {
        _manager = manager;
    }

    public async Task<EditResultDto> UpdateQuestionAsync(UpdateQuestionInput input)
    {
        var texts = input.Options.GroupBy(o => o.Code).ToDictionary(g => g.Key, g => g.Last().Text);
        if (input.Options.Count != texts.Count) throw new UserFriendlyException("Mã lựa chọn bị lặp.");
        var versionId = await _manager.UpdateQuestionAsync(input.QuestionId, input.Stem, input.Explanation,
            input.ShuffleOptions, input.CorrectOptionCode, texts);
        return await Revalidate(versionId);
    }

    public async Task<EditResultDto> UpdateGroupAsync(UpdateGroupInput input)
    {
        return await Revalidate(await _manager.UpdateGroupAsync(input.GroupId, input.ShuffleQuestions));
    }

    public async Task<EditResultDto> UpdatePartAsync(UpdatePartInput input)
    {
        return await Revalidate(await _manager.UpdatePartAsync(input.PartId, input.ShuffleItems));
    }

    public async Task<EditResultDto> UpdateBlockTextAsync(UpdateBlockTextInput input)
    {
        return await Revalidate(await _manager.UpdateBlockTextAsync(input.BlockId, input.Text));
    }

    public async Task<EditResultDto> UpdateSettingsAsync(UpdateSettingsInput input)
    {
        return await Revalidate(await _manager.UpdateSettingsAsync(input.VersionId, input.Title, input.DurationMinutes));
    }

    private async Task<EditResultDto> Revalidate(long versionId)
    {
        // Lưu các thay đổi đang theo dõi rồi kiểm tra lại trên dữ liệu vừa sửa.
        await CurrentUnitOfWork.SaveChangesAsync();
        var version = await _manager.GetFullAsync(versionId);
        return new EditResultDto { Issues = _manager.Validate(version).Select(e => e.ToDto()).ToList() };
    }
}
