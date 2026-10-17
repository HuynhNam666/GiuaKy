using Abp.Authorization;
using Abp.Localization;
using Abp.MultiTenancy;

namespace _2380601390.Authorization;

public class _2380601390AuthorizationProvider : AuthorizationProvider
{
    public override void SetPermissions(IPermissionDefinitionContext context)
    {
        context.CreatePermission(PermissionNames.Pages_Users, L("Users"));
        context.CreatePermission(PermissionNames.Pages_Users_Activation, L("UsersActivation"));
        context.CreatePermission(PermissionNames.Pages_Roles, L("Roles"));
        var exams = context.CreatePermission(PermissionNames.Pages_Exams, L("Exams"));
        exams.CreateChildPermission(PermissionNames.Pages_Exams_Manage, L("ExamsManage"));
        exams.CreateChildPermission(PermissionNames.Pages_Exams_Publish, L("ExamsPublish"));
        exams.CreateChildPermission(PermissionNames.Pages_Exams_Statistics, L("ExamsStatistics"));
        context.CreatePermission(PermissionNames.Pages_Attempts, L("Attempts"));
        context.CreatePermission(PermissionNames.Pages_Tenants, L("Tenants"), multiTenancySides: MultiTenancySides.Host);
    }

    private static ILocalizableString L(string name)
    {
        return new LocalizableString(name, _2380601390Consts.LocalizationSourceName);
    }
}
