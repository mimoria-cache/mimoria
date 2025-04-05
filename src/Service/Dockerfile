FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app
# Workaround until https://github.com/testcontainers/testcontainers-dotnet/pull/1397 is released
RUN chown app:app /app
USER $APP_UID
ENV DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1
ENV DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=0

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
#RUN apt update
#RUN apt install -y clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Service/Service.csproj", "src/Service/"]
COPY ["src/Server/Server.csproj", "src/Server/"]
COPY ["src/Core/Core.csproj", "src/Core/"]
RUN dotnet restore "./src/Service/Service.csproj"
COPY . .
WORKDIR "/src/src/Service"
RUN dotnet build "./Service.csproj" -c Release -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
#ENTRYPOINT ["./Varelen.Mimoria.Service"]
ENTRYPOINT ["dotnet", "Varelen.Mimoria.Service.dll"]
