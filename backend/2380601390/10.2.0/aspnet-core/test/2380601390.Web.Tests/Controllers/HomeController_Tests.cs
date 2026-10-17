using _2380601390.Models.TokenAuth;
using _2380601390.Web.Controllers;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace _2380601390.Web.Tests.Controllers;

public class HomeController_Tests : _2380601390WebTestBase
{
    [Fact]
    public async Task Index_Test()
    {
        await AuthenticateAsync(null, new AuthenticateModel
        {
            UserNameOrEmailAddress = "admin",
            Password = "123qwe"
        });

        //Act
        var response = await GetResponseAsStringAsync(
            GetUrl<HomeController>(nameof(HomeController.Index))
        );

        //Assert
        response.ShouldNotBeNullOrEmpty();
    }
}