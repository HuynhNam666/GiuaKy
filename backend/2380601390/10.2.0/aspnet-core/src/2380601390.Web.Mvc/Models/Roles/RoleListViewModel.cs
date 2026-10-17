using _2380601390.Roles.Dto;
using System.Collections.Generic;

namespace _2380601390.Web.Models.Roles;

public class RoleListViewModel
{
    public IReadOnlyList<PermissionDto> Permissions { get; set; }
}
