using _2380601390.Roles.Dto;
using System.Collections.Generic;

namespace _2380601390.Web.Models.Common;

public interface IPermissionsEditViewModel
{
    List<FlatPermissionDto> Permissions { get; set; }
}