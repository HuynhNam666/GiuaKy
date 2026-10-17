using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace _2380601390.EntityFrameworkCore;

public static class _2380601390DbContextConfigurer
{
    public static void Configure(DbContextOptionsBuilder<_2380601390DbContext> builder, string connectionString)
    {
        builder.UseSqlServer(connectionString);
    }

    public static void Configure(DbContextOptionsBuilder<_2380601390DbContext> builder, DbConnection connection)
    {
        builder.UseSqlServer(connection);
    }
}
