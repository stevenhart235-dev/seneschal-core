# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Seneschal.Core/Seneschal.Core.csproj Seneschal.Core/
COPY Seneschal.Persistence.PostgreSql/Seneschal.Persistence.PostgreSql.csproj Seneschal.Persistence.PostgreSql/
COPY Seneschal.Api/Seneschal.Api.csproj Seneschal.Api/
RUN dotnet restore Seneschal.Api/Seneschal.Api.csproj

COPY Seneschal.Core/ Seneschal.Core/
COPY Seneschal.Persistence.PostgreSql/ Seneschal.Persistence.PostgreSql/
COPY Seneschal.Api/ Seneschal.Api/
RUN dotnet publish Seneschal.Api/Seneschal.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    HOME=/home/app

COPY --from=build /app/publish .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID:$APP_UID /home/app

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3 && grep -q "200 OK" <&3'

ENTRYPOINT ["dotnet", "Seneschal.Api.dll"]
