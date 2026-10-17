using _2380601390.Roles.Dto;
using System.Collections.Generic;

namespace _2380601390.Web.Models.Users;

public class UserListViewModel
{
    public IReadOnlyList<RoleDto> Roles { get; set; }
}
