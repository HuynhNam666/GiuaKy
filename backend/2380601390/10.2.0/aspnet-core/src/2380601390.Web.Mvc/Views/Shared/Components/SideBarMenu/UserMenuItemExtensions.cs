using Abp.Application.Navigation;
using System.Collections.Generic;
using System.Linq;

namespace _2380601390.Web.Views.Shared.Components.SideBarMenu;

public static class UserMenuItemExtensions
{
    public static IOrderedEnumerable<UserMenuItem> OrderByCustom(this IEnumerable<UserMenuItem> menuItems)
    {
        return menuItems
            .OrderBy(menuItem => menuItem.Order)
            .ThenBy(menuItem => menuItem.DisplayName);
    }
}
