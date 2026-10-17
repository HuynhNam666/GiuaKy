using Abp.Authorization;
using _2380601390.Authorization.Roles;
using _2380601390.Authorization.Users;

namespace _2380601390.Authorization;

public class PermissionChecker : PermissionChecker<Role, User>
{
    public PermissionChecker(UserManager userManager)
        : base(userManager)
    {
    }
}
