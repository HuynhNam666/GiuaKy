using System.ComponentModel.DataAnnotations;

namespace _2380601390.Configuration.Dto;

public class ChangeUiThemeInput
{
    [Required]
    [StringLength(32)]
    public string Theme { get; set; }
}
