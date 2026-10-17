using Abp.AspNetCore;
using Abp.AspNetCore.TestBase;
using Abp.Modules;
using Abp.Reflection.Extensions;
using _2380601390.EntityFrameworkCore;
using _2380601390.Web.Startup;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace _2380601390.Web.Tests;

[DependsOn(
    typeof(_2380601390WebMvcModule),
    typeof(AbpAspNetCoreTestBaseModule)
)]
public class _2380601390WebTestModule : AbpModule
{
    public _2380601390WebTestModule(_2380601390EntityFrameworkModule abpProjectNameEntityFrameworkModule)
    {
        abpProjectNameEntityFrameworkModule.SkipDbContextRegistration = true;
    }

    public override void PreInitialize()
    {
        Configuration.UnitOfWork.IsTransactional = false; //EF Core InMemory DB does not support transactions.
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(_2380601390WebTestModule).GetAssembly());
    }

    public override void PostInitialize()
    {
        IocManager.Resolve<ApplicationPartManager>()
            .AddApplicationPartsIfNotAddedBefore(typeof(_2380601390WebMvcModule).Assembly);
    }
}