FROM mcr.microsoft.com/dotnet/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 8090

FROM mcr.microsoft.com/dotnet/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Preferred.Api/Preferred.Api.csproj", "Preferred.Api/"]
COPY ["Preferred.Api/template/tag_import.xlsx", "Preferred.Api/template/"]
RUN dotnet restore "Preferred.Api/Preferred.Api.csproj"
COPY . .
WORKDIR /src/Preferred.Api
RUN dotnet build "Preferred.Api.csproj" -p:Version=23.0.0-docker -c Release -o /app/build
FROM build AS publish
RUN dotnet publish "Preferred.Api.csproj" -p:Version=23.0.0-docker -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPoint ["dotnet", "Preferred.Api.dll"]