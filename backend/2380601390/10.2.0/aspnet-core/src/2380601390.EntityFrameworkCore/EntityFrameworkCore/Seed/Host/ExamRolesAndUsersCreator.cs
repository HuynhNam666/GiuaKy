using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using _2380601390.Authorization;
using _2380601390.Authorization.Roles;
using _2380601390.Authorization.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;

namespace _2380601390.EntityFrameworkCore.Seed.Host;

/// <summary>
/// Tạo role Teacher/Student (host) + 2 tài khoản demo. Mật khẩu demo theo quy ước của template (123qwe).
/// </summary>
public class ExamRolesAndUsersCreator
{
    private readonly _2380601390DbContext _context;

    public ExamRolesAndUsersCreator(_2380601390DbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        var teacherRole = EnsureRole(StaticRoleNames.Teacher, new[]
        {
            PermissionNames.Pages_Exams,
            PermissionNames.Pages_Exams_Manage,
            PermissionNames.Pages_Exams_Publish,
            PermissionNames.Pages_Exams_Statistics
        });
        var studentRole = EnsureRole(StaticRoleNames.Student, new[] { PermissionNames.Pages_Attempts });

        EnsureUser("giangvien", "Giảng", "Viên", "giangvien@toeic.local", teacherRole);
        EnsureUser("sinhvien", "Sinh", "Viên", "sinhvien@toeic.local", studentRole);
    }

    private Role EnsureRole(string name, string[] permissions)
    {
        var role = _context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == null && r.Name == name);
        if (role == null)
        {
            role = _context.Roles.Add(new Role(null, name, name) { IsStatic = false }).Entity;
            _context.SaveChanges();
        }

        var granted = _context.Permissions.IgnoreQueryFilters()
            .OfType<RolePermissionSetting>()
            .Where(p => p.TenantId == null && p.RoleId == role.Id)
            .Select(p => p.Name)
            .ToList();

        var missing = permissions.Where(p => !granted.Contains(p)).ToList();
        if (missing.Any())
        {
            _context.Permissions.AddRange(missing.Select(p => new RolePermissionSetting
            {
                TenantId = null,
                Name = p,
                IsGranted = true,
                RoleId = role.Id
            }));
            _context.SaveChanges();
        }

        return role;
    }

    private void EnsureUser(string userName, string name, string surname, string email, Role role)
    {
        var user = _context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.TenantId == null && u.UserName == userName);
        if (user != null)
        {
            return;
        }

        user = new User
        {
            TenantId = null,
            UserName = userName,
            Name = name,
            Surname = surname,
            EmailAddress = email,
            IsEmailConfirmed = true,
            IsActive = true
        };
        user.Password = new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
            .HashPassword(user, "123qwe");
        user.SetNormalizedNames();

        user = _context.Users.Add(user).Entity;
        _context.SaveChanges();

        _context.UserRoles.Add(new UserRole(null, user.Id, role.Id));
        _context.SaveChanges();
    }
}
