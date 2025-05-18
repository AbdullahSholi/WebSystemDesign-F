FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["RabbitMQ-MongoDB-Consumer/RabbitMQ-MongoDB-Consumer.csproj", "RabbitMQ-MongoDB-Consumer/"]
COPY ["SignalRHub/SignalRHub.csproj", "SignalRHub/"]
RUN dotnet restore "RabbitMQ-MongoDB-Consumer/RabbitMQ-MongoDB-Consumer.csproj"

COPY . .
WORKDIR "/src/RabbitMQ-MongoDB-Consumer"
RUN dotnet build "RabbitMQ-MongoDB-Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/RabbitMQ-MongoDB-Consumer"
RUN dotnet publish "RabbitMQ-MongoDB-Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Overwrite the default appsettings.json if needed
COPY ["RabbitMQ-MongoDB-Consumer/appsettings.json", "/app/publish/"]

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RabbitMQ-MongoDB-Consumer.dll"]
