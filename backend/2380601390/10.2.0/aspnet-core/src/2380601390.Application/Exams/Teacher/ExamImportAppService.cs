using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using _2380601390.Authorization;
using _2380601390.Exams.Import;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Exams.Teacher;

public interface IExamImportAppService : Abp.Application.Services.IApplicationService
{
    Task<ImportExamResultDto> ImportAsync(ImportExamInput input);
}

[AbpAuthorize(PermissionNames.Pages_Exams_Manage)]
public class ExamImportAppService : _2380601390AppServiceBase, IExamImportAppService
{
    /// <summary>Số câu mà một đề TOEIC Reading phải có (giả định: 100, cần giảng viên xác nhận).</summary>
    public const int ExpectedTotalQuestions = ExamRules.ExpectedTotalQuestions;

    public const int MaxFileBytes = 20 * 1024 * 1024;

    private readonly ExamImportManager _importManager;

    public ExamImportAppService(ExamImportManager importManager)
    {
        _importManager = importManager;
    }

    /// <summary>
    /// Đọc, kiểm tra toàn bộ rồi mới lưu. Lỗi nội dung không ném exception mà trả về danh sách lỗi
    /// và không ghi gì vào CSDL (việc lưu chỉ xảy ra sau khi mọi kiểm tra đạt).
    /// </summary>
    [DisableAuditing]
    public async Task<ImportExamResultDto> ImportAsync(ImportExamInput input)
    {
        var result = new ImportExamResultDto();

        if (!input.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(new ImportErrorDto { Code = "E_FILE_INVALID", Message = "Chỉ chấp nhận tệp có đuôi .docx." });
            return result;
        }

        if (input.Content.Length == 0 || input.Content.Length > MaxFileBytes)
        {
            result.Errors.Add(new ImportErrorDto
            {
                Code = "E_FILE_INVALID",
                Message = input.Content.Length == 0 ? "Tệp rỗng." : "Tệp vượt quá giới hạn " + (MaxFileBytes / 1024 / 1024) + " MB."
            });
            return result;
        }

        ImportResult parsed;
        using (var ms = new MemoryStream(input.Content))
        {
            parsed = ExamDocxImporter.Parse(ms, new ExamImportOptions
            {
                RequireAllParts = true,
                ExpectedTotalQuestions = ExpectedTotalQuestions
            });
        }

        if (!parsed.Success)
        {
            result.Errors = parsed.Errors.Select(e => new ImportErrorDto
            {
                Code = e.Code,
                Message = e.Message,
                Paragraph = e.Paragraph,
                Table = e.Table,
                PartNo = e.PartNo,
                GroupCode = e.GroupCode,
                QuestionNo = e.QuestionNo,
                Tag = e.Tag
            }).ToList();
            return result;
        }

        string fileSha;
        using (var sha = SHA256.Create())
        {
            fileSha = BitConverter.ToString(sha.ComputeHash(input.Content)).Replace("-", "").ToLowerInvariant();
        }

        var version = await _importManager.SaveAsDraftAsync(parsed.Exam, Path.GetFileName(input.FileName), fileSha);

        result.Success = true;
        result.ExamId = version.ExamId;
        result.ExamVersionId = version.Id;
        result.VersionNo = version.VersionNo;
        result.Title = parsed.Exam.Title;
        result.QuestionCount = parsed.Exam.AllQuestions.Count();
        result.ImageCount = parsed.Exam.Parts.SelectMany(p => p.Groups).SelectMany(g => g.Documents)
            .SelectMany(d => d.Blocks).Count(b => b.Type == ContentBlockType.Image);
        return result;
    }
}
