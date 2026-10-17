using Abp.Events.Bus;
using Abp.Modules;
using Abp.Reflection.Extensions;
using _2380601390.Configuration;
using _2380601390.EntityFrameworkCore;
using _2380601390.Migrator.DependencyInjection;
using Castle.MicroKernel.Registration;
using Microsoft.Extensions.Configuration;

namespace _2380601390.Migrator;

[DependsOn(typeof(_2380601390EntityFrameworkModule))]
public class _2380601390MigratorModule : AbpModule
{
    private readonly IConfigurationRoot _appConfiguration;

    public _2380601390MigratorModule(_2380601390EntityFrameworkModule abpProjectNameEntityFrameworkModule)
    {
        abpProjectNameEntityFrameworkModule.SkipDbSeed = true;

        _appConfiguration = AppConfigurations.Get(
            typeof(_2380601390MigratorModule).GetAssembly().GetDirectoryPathOrNull()
        );
    }

    public override void PreInitialize()
    {
        Configuration.DefaultNameOrConnectionString = _appConfiguration.GetConnectionString(
            _2380601390Consts.ConnectionStringName
        );

        Configuration.BackgroundJobs.IsJobExecutionEnabled = false;
        Configuration.ReplaceService(
            typeof(IEventBus),
            () => IocManager.IocContainer.Register(
                Component.For<IEventBus>().Instance(NullEventBus.Instance)
            )
        );
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(_2380601390MigratorModule).GetAssembly());
        ServiceCollectionRegistrar.Register(IocManager);
    }
}
