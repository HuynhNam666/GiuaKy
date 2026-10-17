using Abp.AspNetCore.Mvc.ViewComponents;

namespace _2380601390.Web.Views;

public abstract class _2380601390ViewComponent : AbpViewComponent
{
    protected _2380601390ViewComponent()
    {
        LocalizationSourceName = _2380601390Consts.LocalizationSourceName;
    }
}
