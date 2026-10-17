using Abp.AspNetCore.Mvc.Authorization;
using _2380601390.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace _2380601390.Web.Controllers;

[AbpMvcAuthorize]
public class AboutController : _2380601390ControllerBase
{
    public ActionResult Index()
    {
        return View();
    }
}
