using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Domain.Repositories;
using _2380601390.Authorization;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Exams.Teacher;

public interface IExamAppService : Abp.Application.Services.IApplicationService
{
    Task<List<ExamVersionListItemDto>> GetVersionsAsync();

    Task<ExamPreviewDto> GetPreviewAsync(long versionId);
}

[AbpAuthorize(PermissionNames.Pages_Exams_Manage)]
public class ExamAppService : _2380601390AppServiceBase, IExamAppService
{
    private readonly IRepository<ExamVersion, long> _versions;
    private readonly ExamVersionManager _manager;

    public ExamAppService(IRepository<ExamVersion, long> versions, ExamVersionManager manager)
    {
        _versions = versions;
        _manager = manager;
    }

    /// <summary>Danh sách mọi phiên bản đề (mới nhất trước) cho trang quản lý của giảng viên.</summary>
    public Task<List<ExamVersionListItemDto>> GetVersionsAsync()
    {
        var items = _versions.GetAll()
            .OrderByDescending(v => v.CreationTime)
            .Select(v => new ExamVersionListItemDto
            {
                ExamId = v.ExamId,
                VersionId = v.Id,
                Code = v.Exam.Code,
                Title = v.Exam.Title,
                VersionNo = v.VersionNo,
                Status = v.Status,
                DurationMinutes = v.DurationMinutes,
                TotalQuestions = v.TotalQuestions,
                SourceFileName = v.SourceFileName,
                CreationTime = v.CreationTime
            })
            .ToList();
        return Task.FromResult(items);
    }

    /// <summary>Toàn bộ nội dung một phiên bản (văn bản, ảnh, nhóm, câu hỏi, đáp án) để giảng viên rà soát.</summary>
    public async Task<ExamPreviewDto> GetPreviewAsync(long versionId)
    {
        var v = await _manager.GetFullAsync(versionId);
        var dto = new ExamPreviewDto
        {
            VersionId = v.Id,
            ExamId = v.ExamId,
            Code = v.Exam.Code,
            Title = v.Exam.Title,
            VersionNo = v.VersionNo,
            Status = v.Status,
            DurationMinutes = v.DurationMinutes,
            TotalQuestions = v.TotalQuestions,
            SourceFileName = v.SourceFileName,
            CanEdit = v.Status == ExamVersionStatus.Draft,
            Issues = _manager.Validate(v).Select(e => e.ToDto()).ToList()
        };

        foreach (var p in v.Parts.OrderBy(x => x.DisplayOrder))
        {
            var pd = new PartPreviewDto { Id = p.Id, PartNo = p.PartNo, FromNo = p.FromNo, ToNo = p.ToNo, ShuffleItems = p.ShuffleItems };
            pd.Questions = p.Questions.Where(q => q.ReadingGroupId == null).OrderBy(q => q.SourceOrder).Select(ToDto).ToList();
            foreach (var g in p.Groups.OrderBy(x => x.DisplayOrder))
            {
                pd.Groups.Add(new GroupPreviewDto
                {
                    Id = g.Id,
                    Code = g.Code,
                    ShuffleQuestions = g.ShuffleQuestions,
                    Documents = g.Documents.OrderBy(d => d.DisplayOrder).Select(d => new DocumentPreviewDto
                    {
                        Id = d.Id,
                        Code = d.Code,
                        Title = d.Title,
                        Blocks = d.Blocks.OrderBy(b => b.DisplayOrder).Select(b => new BlockPreviewDto
                        {
                            Id = b.Id,
                            Type = b.BlockType,
                            Text = b.Text,
                            MediaAssetId = b.MediaAssetId,
                            AltText = b.AltText
                        }).ToList()
                    }).ToList(),
                    Questions = p.Questions.Where(q => q.ReadingGroupId == g.Id).OrderBy(q => q.SourceOrder).Select(ToDto).ToList()
                });
            }

            dto.Parts.Add(pd);
        }

        return dto;
    }

    private static QuestionPreviewDto ToDto(Question q) => new QuestionPreviewDto
    {
        Id = q.Id,
        Code = q.Code,
        SourceNo = q.SourceNo,
        Stem = q.StemText,
        Explanation = q.Explanation,
        ShuffleOptions = q.ShuffleOptions,
        CorrectOptionCode = q.Options.FirstOrDefault(o => o.Id == q.CorrectOptionId)?.Code,
        Options = q.Options.OrderBy(o => o.SourceOrder).Select(o => new OptionPreviewDto { Id = o.Id, Code = o.Code, Text = o.Text }).ToList()
    };
}
