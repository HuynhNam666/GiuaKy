using System.Collections.Generic;
using _2380601390.Exams.Teacher.Dto;

namespace _2380601390.Web.Models.Exams;

public class ExamListViewModel
{
    public List<ExamVersionListItemDto> Versions { get; set; } = new List<ExamVersionListItemDto>();

    /// <summary>Lỗi của lần upload vừa rồi (nếu có). Khi có lỗi, không có dữ liệu nào được lưu.</summary>
    public List<ImportErrorDto> Errors { get; set; } = new List<ImportErrorDto>();

    public string UploadedFileName { get; set; }
    public string SuccessMessage { get; set; }
}

public class ExamStatisticsViewModel
{
    public System.Collections.Generic.List<_2380601390.Exams.Teacher.Dto.ExamVersionListItemDto> Versions { get; set; } = new System.Collections.Generic.List<_2380601390.Exams.Teacher.Dto.ExamVersionListItemDto>();
    public _2380601390.Exams.Teacher.ExamStatisticsDto Stats { get; set; }
}
