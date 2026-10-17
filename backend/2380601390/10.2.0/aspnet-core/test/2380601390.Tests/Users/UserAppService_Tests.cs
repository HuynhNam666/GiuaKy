using _2380601390.Users;
using _2380601390.Users.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace _2380601390.Tests.Users;

public class UserAppService_Tests : _2380601390TestBase
{
    private readonly IUserAppService _userAppService;

    public UserAppService_Tests()
    {
        _userAppService = Resolve<IUserAppService>();
    }

    [Fact]
    public async Task GetUsers_Test()
    {
        // Act
        var output = await _userAppService.GetAllAsync(new PagedUserResultRequestDto { MaxResultCount = 20, SkipCount = 0 });

        // Assert
        output.Items.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateUser_Test()
    {
        // Act
        await _userAppService.CreateAsync(
            new CreateUserDto
            {
                EmailAddress = "john@volosoft.com",
                IsActive = true,
                Name = "John",
                Surname = "Nash",
                Password = "123qwe",
                UserName = "john.nash"
            });

        await UsingDbContextAsync(async context =>
        {
            var johnNashUser = await context.Users.FirstOrDefaultAsync(u => u.UserName == "john.nash");
            johnNashUser.ShouldNotBeNull();
        });
    }
}
