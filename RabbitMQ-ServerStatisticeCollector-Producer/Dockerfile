FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["RabbitMQ-ServerStatisticeCollector-Producer/RabbitMQ-ServerStatisticeCollector-Producer.csproj", "RabbitMQ-ServerStatisticeCollector-Producer/"]
RUN dotnet restore "RabbitMQ-ServerStatisticeCollector-Producer/RabbitMQ-ServerStatisticeCollector-Producer.csproj"
COPY . .
WORKDIR "/src/RabbitMQ-ServerStatisticeCollector-Producer"
RUN dotnet build "RabbitMQ-ServerStatisticeCollector-Producer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RabbitMQ-ServerStatisticeCollector-Producer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQ-ServerStatisticeCollector-Producer.dll"]
