FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY ["src/2380601390.Web.Host/2380601390.Web.Host.csproj", "src/2380601390.Web.Host/"]
COPY ["src/2380601390.Web.Core/2380601390.Web.Core.csproj", "src/2380601390.Web.Core/"]
COPY ["src/2380601390.Application/2380601390.Application.csproj", "src/2380601390.Application/"]
COPY ["src/2380601390.Core/2380601390.Core.csproj", "src/2380601390.Core/"]
COPY ["src/2380601390.EntityFrameworkCore/2380601390.EntityFrameworkCore.csproj", "src/2380601390.EntityFrameworkCore/"]
WORKDIR "/src/src/2380601390.Web.Host"
RUN dotnet restore 

WORKDIR /src
COPY ["src/2380601390.Web.Host", "src/2380601390.Web.Host"]
COPY ["src/2380601390.Web.Core", "src/2380601390.Web.Core"]
COPY ["src/2380601390.Application", "src/2380601390.Application"]
COPY ["src/2380601390.Core", "src/2380601390.Core"]
COPY ["src/2380601390.EntityFrameworkCore", "src/2380601390.EntityFrameworkCore"]
WORKDIR "/src/src/2380601390.Web.Host"
RUN dotnet publish -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 80
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "2380601390.Web.Host.dll"]
