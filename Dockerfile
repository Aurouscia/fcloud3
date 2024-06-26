# docker部署仍在开发中，没有做存储持久化，**请勿使用**

# 构建vue前端（可优化，先取出package.json install，避免其他改动造成重下npm包）
FROM node:18-alpine AS febuild
WORKDIR "/app"
COPY . .
WORKDIR "/app/FCloud3.AppFront/FCloud3Front"
RUN npm install
RUN npm run build

# 构建.net后端（可优化）
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS bebuild
WORKDIR "/src"
COPY . .
WORKDIR "/src/FCloud3.App"
RUN dotnet publish "./FCloud3.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 组合进.net8.0运行环境
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# 绕过老版sqlserver协议问题 A connection was successfully established with the server, but then an error occurred during the pre-login handshake. (provider: SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed)
RUN sed -i 's|\[openssl_init\]|&\nssl_conf = ssl_configuration\n[ssl_configuration]\nsystem_default = tls_system_default\n[tls_system_default]\nMinProtocol = TLSv1\nCipherString = DEFAULT@SECLEVEL=0|' /etc/ssl/openssl.cnf
WORKDIR "/app"
COPY --from=bebuild /app/publish .
COPY --from=febuild /app/FCloud3.App/wwwroot ./wwwroot
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "FCloud3.App.dll"]