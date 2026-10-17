using Abp.AspNetCore.Mvc.Views;
using Abp.Runtime.Session;
using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace _2380601390.Web.Views;

public abstract class _2380601390RazorPage<TModel> : AbpRazorPage<TModel>
{
    [RazorInject]
    public IAbpSession AbpSession { get; set; }

    protected _2380601390RazorPage()
    {
        LocalizationSourceName = _2380601390Consts.LocalizationSourceName;
    }
}
