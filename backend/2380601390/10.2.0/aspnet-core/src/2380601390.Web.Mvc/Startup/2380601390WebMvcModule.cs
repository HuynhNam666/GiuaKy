using Abp.Modules;
using Abp.Reflection.Extensions;
using _2380601390.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace _2380601390.Web.Startup;

[DependsOn(typeof(_2380601390WebCoreModule))]
public class _2380601390WebMvcModule : AbpModule
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfigurationRoot _appConfiguration;

    public _2380601390WebMvcModule(IWebHostEnvironment env)
    {
        _env = env;
        _appConfiguration = env.GetAppConfiguration();
    }

    public override void PreInitialize()
    {
        Configuration.Navigation.Providers.Add<_2380601390NavigationProvider>();
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(_2380601390WebMvcModule).GetAssembly());
    }
}
