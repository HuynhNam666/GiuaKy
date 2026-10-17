using _2380601390.Configuration;
using _2380601390.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace _2380601390.EntityFrameworkCore;

/* This class is needed to run "dotnet ef ..." commands from command line on development. Not used anywhere else */
public class _2380601390DbContextFactory : IDesignTimeDbContextFactory<_2380601390DbContext>
{
    public _2380601390DbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<_2380601390DbContext>();

        /*
         You can provide an environmentName parameter to the AppConfigurations.Get method. 
         In this case, AppConfigurations will try to read appsettings.{environmentName}.json.
         Use Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") method or from string[] args to get environment if necessary.
         https://docs.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#args
         */
        var configuration = AppConfigurations.Get(WebContentDirectoryFinder.CalculateContentRootFolder());

        _2380601390DbContextConfigurer.Configure(builder, configuration.GetConnectionString(_2380601390Consts.ConnectionStringName));

        return new _2380601390DbContext(builder.Options);
    }
}
