FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["RankCalculator/RankCalculator.csproj", "RankCalculator/"]
COPY ["ConnectionLibrary/ConnectionLibrary.csproj", "ConnectionLibrary/"]

RUN dotnet restore "RankCalculator/RankCalculator.csproj"

COPY . .
WORKDIR "/src/RankCalculator"
RUN dotnet build "RankCalculator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RankCalculator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RankCalculator.dll"]