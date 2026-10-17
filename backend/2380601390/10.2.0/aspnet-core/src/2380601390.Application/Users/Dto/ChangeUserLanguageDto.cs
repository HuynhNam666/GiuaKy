using System.ComponentModel.DataAnnotations;

namespace _2380601390.Users.Dto;

public class ChangeUserLanguageDto
{
    [Required]
    public string LanguageName { get; set; }
}