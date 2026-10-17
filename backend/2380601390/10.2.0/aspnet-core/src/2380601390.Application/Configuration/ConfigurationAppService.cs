using Abp.Authorization;
using Abp.Runtime.Session;
using _2380601390.Configuration.Dto;
using System.Threading.Tasks;

namespace _2380601390.Configuration;

[AbpAuthorize]
public class ConfigurationAppService : _2380601390AppServiceBase, IConfigurationAppService
{
    public async Task ChangeUiTheme(ChangeUiThemeInput input)
    {
        await SettingManager.ChangeSettingForUserAsync(AbpSession.ToUserIdentifier(), AppSettingNames.UiTheme, input.Theme);
    }
}
