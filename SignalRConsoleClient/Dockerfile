FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SignalRConsoleClient/SignalRConsoleClient.csproj", "SignalRConsoleClient/"]
RUN dotnet restore "SignalRConsoleClient/SignalRConsoleClient.csproj"
COPY . .
WORKDIR "/src/SignalRConsoleClient"
RUN dotnet build "SignalRConsoleClient.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SignalRConsoleClient.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SignalRConsoleClient.dll"]
