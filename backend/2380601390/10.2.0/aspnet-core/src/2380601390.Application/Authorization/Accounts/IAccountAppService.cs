using Abp.Application.Services;
using _2380601390.Authorization.Accounts.Dto;
using System.Threading.Tasks;

namespace _2380601390.Authorization.Accounts;

public interface IAccountAppService : IApplicationService
{
    Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input);

    Task<RegisterOutput> Register(RegisterInput input);
}
