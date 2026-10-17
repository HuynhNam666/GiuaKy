using Abp.AspNetCore.Mvc.Controllers;
using Abp.IdentityFramework;
using Microsoft.AspNetCore.Identity;

namespace _2380601390.Controllers
{
    public abstract class _2380601390ControllerBase : AbpController
    {
        protected _2380601390ControllerBase()
        {
            LocalizationSourceName = _2380601390Consts.LocalizationSourceName;
        }

        protected void CheckErrors(IdentityResult identityResult)
        {
            identityResult.CheckErrors(LocalizationManager);
        }
    }
}
