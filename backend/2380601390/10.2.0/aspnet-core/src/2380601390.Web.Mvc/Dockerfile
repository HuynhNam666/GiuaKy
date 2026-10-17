FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
RUN ln -s ~/.dotnet/tools/libman /usr/local/bin/libman

WORKDIR /src
COPY ["src/2380601390.Web.Mvc/2380601390.Web.Mvc.csproj", "src/2380601390.Web.Mvc/"]
COPY ["src/2380601390.Web.Core/2380601390.Web.Core.csproj", "src/2380601390.Web.Core/"]
COPY ["src/2380601390.Application/2380601390.Application.csproj", "src/2380601390.Application/"]
COPY ["src/2380601390.Core/2380601390.Core.csproj", "src/2380601390.Core/"]
COPY ["src/2380601390.EntityFrameworkCore/2380601390.EntityFrameworkCore.csproj", "src/2380601390.EntityFrameworkCore/"]
WORKDIR "/src/src/2380601390.Web.Mvc"
RUN dotnet restore 

WORKDIR /src
COPY ["src/2380601390.Web.Mvc", "src/2380601390.Web.Mvc"]
COPY ["src/2380601390.Web.Core", "src/2380601390.Web.Core"]
COPY ["src/2380601390.Application", "src/2380601390.Application"]
COPY ["src/2380601390.Core", "src/2380601390.Core"]
COPY ["src/2380601390.EntityFrameworkCore", "src/2380601390.EntityFrameworkCore"]
WORKDIR "/src/src/2380601390.Web.Mvc"
RUN libman restore
RUN dotnet publish -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 80
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "2380601390.Web.Mvc.dll"]
