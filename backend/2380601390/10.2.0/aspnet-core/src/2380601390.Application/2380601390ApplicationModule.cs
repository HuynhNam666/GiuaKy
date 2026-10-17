using Abp.AutoMapper;
using Abp.Modules;
using Abp.Threading.BackgroundWorkers;
using _2380601390.Exams.Student;
using Abp.Reflection.Extensions;
using _2380601390.Authorization;

namespace _2380601390;

[DependsOn(
    typeof(_2380601390CoreModule),
    typeof(AbpAutoMapperModule))]
public class _2380601390ApplicationModule : AbpModule
{
    public override void PreInitialize()
    {
        Configuration.Authorization.Providers.Add<_2380601390AuthorizationProvider>();
    }

    public override void Initialize()
    {
        var thisAssembly = typeof(_2380601390ApplicationModule).GetAssembly();

        IocManager.RegisterAssemblyByConvention(thisAssembly);

        Configuration.Modules.AbpAutoMapper().Configurators.Add(
            // Scan the assembly for classes which inherit from AutoMapper.Profile
            cfg => cfg.AddMaps(thisAssembly)
        );
    }

    public override void PostInitialize()
    {
        // Tự thu bài các lượt thi hết hạn kể cả khi client đã đóng.
        IocManager.Resolve<IBackgroundWorkerManager>().Add(IocManager.Resolve<ExpiredAttemptWorker>());
    }
}
