FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/AuthSystem.Api/AuthSystem.Api.csproj", "src/AuthSystem.Api/"]
COPY ["src/AuthSystem.Application/AuthSystem.Application.csproj", "src/AuthSystem.Application/"]
COPY ["src/AuthSystem.Infrastructure/AuthSystem.Infrastructure.csproj", "src/AuthSystem.Infrastructure/"]
COPY ["src/AuthSystem.Domain/AuthSystem.Domain.csproj", "src/AuthSystem.Domain/"]
RUN dotnet restore "src/AuthSystem.Api/AuthSystem.Api.csproj"
COPY . .
WORKDIR "/src/src/AuthSystem.Api"
RUN dotnet build "AuthSystem.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AuthSystem.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthSystem.Api.dll"]

# Dev stage: no COPY of source - docker-compose.override.yml bind-mounts the backend/
# folder over /src at runtime so `dotnet watch` picks up file changes from the host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# vsdbg lets an IDE (VS Code's coreclr debugger via pipeTransport/docker exec, or VS's
# "Attach to Process (Docker)") attach to the dotnet process this container is already
# running, instead of launching a separate throwaway debug container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl unzip \
    && curl -sSL https://aka.ms/getvsdbgsh -o /tmp/GetVsDbg.sh \
    && bash /tmp/GetVsDbg.sh -v latest -l /vsdbg \
    && rm /tmp/GetVsDbg.sh \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src/src/AuthSystem.Api
EXPOSE 8080
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--non-interactive", "--urls", "http://+:8080"]
