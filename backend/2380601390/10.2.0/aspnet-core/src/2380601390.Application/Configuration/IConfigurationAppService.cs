using _2380601390.Configuration.Dto;
using System.Threading.Tasks;

namespace _2380601390.Configuration;

public interface IConfigurationAppService
{
    Task ChangeUiTheme(ChangeUiThemeInput input);
}
