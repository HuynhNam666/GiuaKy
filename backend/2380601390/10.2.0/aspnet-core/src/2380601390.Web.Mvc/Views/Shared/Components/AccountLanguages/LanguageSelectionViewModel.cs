using Abp.Localization;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace _2380601390.Web.Views.Shared.Components.AccountLanguages;

public class LanguageSelectionViewModel
{
    public LanguageInfo CurrentLanguage { get; set; }

    public IReadOnlyList<LanguageInfo> Languages { get; set; }

    public PathString CurrentUrl { get; set; }
}
