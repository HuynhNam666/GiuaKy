using Abp.Application.Services;
using _2380601390.MultiTenancy.Dto;

namespace _2380601390.MultiTenancy;

public interface ITenantAppService : IAsyncCrudAppService<TenantDto, int, PagedTenantResultRequestDto, CreateTenantDto, TenantDto>
{
}

