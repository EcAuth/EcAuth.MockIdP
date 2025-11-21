# デバッグ コンテナーをカスタマイズする方法と、Visual Studio がこの Dockerfile を使用してより高速なデバッグのためにイメージをビルドする方法については、https://aka.ms/customizecontainer をご覧ください。

# このステージは、VS から高速モードで実行するときに使用されます (デバッグ構成の既定値)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# 開発環境用の証明書設定
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev-certs
RUN mkdir -p /https && \
    dotnet dev-certs https -ep /https/aspnetapp.pfx -p changeit

FROM base AS base-with-certs
COPY --from=dev-certs /https/aspnetapp.pfx /https/aspnetapp.pfx
USER $APP_UID


# このステージは、サービス プロジェクトのビルドに使用されます
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG GITHUB_TOKEN
WORKDIR /src

# nuget.configをコピーしてGitHub Packages認証を設定
COPY ["nuget.config", "."]
RUN if [ ! -z "$GITHUB_TOKEN" ]; then \
    dotnet nuget add source https://nuget.pkg.github.com/EcAuth/index.json \
    --name github \
    --username docker \
    --password $GITHUB_TOKEN \
    --store-password-in-clear-text; \
    fi

COPY ["src/MockOpenIdProvider/MockOpenIdProvider.csproj", "MockOpenIdProvider/"]
RUN dotnet restore "./MockOpenIdProvider/MockOpenIdProvider.csproj"
COPY ["src/MockOpenIdProvider/", "MockOpenIdProvider/"]
WORKDIR "/src/MockOpenIdProvider"
RUN dotnet build "./MockOpenIdProvider.csproj" -c $BUILD_CONFIGURATION -o /app/build

# このステージは、最終ステージにコピーするサービス プロジェクトを公開するために使用されます
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MockOpenIdProvider.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# このステージは、運用環境または VS から通常モードで実行している場合に使用されます (デバッグ構成を使用しない場合の既定)
FROM base-with-certs AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MockOpenIdProvider.dll"]

# Linux環境用: mkcertを使用した証明書生成ステージ（最後に配置してWindows環境への影響を回避）
FROM golang:1.21-alpine AS cert-generator
RUN apk add --no-cache git
RUN go install filippo.io/mkcert@latest
RUN mkcert -install
RUN mkdir -p /certs
RUN mkcert -cert-file /certs/localhost.crt -key-file /certs/localhost.key localhost 127.0.0.1 ::1

# Linux環境用: 証明書をPFX形式に変換するステージ
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS pfx-converter
WORKDIR /certs
COPY --from=cert-generator /certs/localhost.crt /certs/localhost.key ./
RUN apt-get update && apt-get install -y openssl && \
    openssl pkcs12 -export \
    -out aspnetapp.pfx \
    -inkey localhost.key \
    -in localhost.crt \
    -passout pass:changeit

# Linux環境用: HTTPS証明書付きの最終ステージ
FROM base AS final-with-https
WORKDIR /app
COPY --from=publish /app/publish .

# 証明書をコピーし、権限を設定
USER root
RUN mkdir -p /https
COPY --from=pfx-converter /certs/aspnetapp.pfx /https/aspnetapp.pfx
RUN chmod 644 /https/aspnetapp.pfx && chown $APP_UID:$APP_UID /https/aspnetapp.pfx
USER $APP_UID

ENTRYPOINT ["dotnet", "MockOpenIdProvider.dll"]