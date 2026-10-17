using _2380601390.Roles.Dto;
using _2380601390.Users.Dto;
using System.Collections.Generic;
using System.Linq;

namespace _2380601390.Web.Models.Users;

public class EditUserModalViewModel
{
    public UserDto User { get; set; }

    public IReadOnlyList<RoleDto> Roles { get; set; }

    public bool UserIsInRole(RoleDto role)
    {
        return User.RoleNames != null && User.RoleNames.Any(r => r == role.NormalizedName);
    }
}
