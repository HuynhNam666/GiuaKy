using Abp.Application.Services;
using _2380601390.Sessions.Dto;
using System.Threading.Tasks;

namespace _2380601390.Sessions;

public interface ISessionAppService : IApplicationService
{
    Task<GetCurrentLoginInformationsOutput> GetCurrentLoginInformations();
}
