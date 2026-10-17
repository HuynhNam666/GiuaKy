using Abp.MultiTenancy;
using _2380601390.Authorization.Users;

namespace _2380601390.MultiTenancy;

public class Tenant : AbpTenant<User>
{
    public Tenant()
    {
    }

    public Tenant(string tenancyName, string name)
        : base(tenancyName, name)
    {
    }
}
