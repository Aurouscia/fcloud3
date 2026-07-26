# 请启动时务必将/app/Data目录mount到宿主机，或设为volumn
# docker部署遇到任何问题请提出issue，或加入readme中的qq群反馈

# 预收集渲染器资源（与前端业务源码解耦，可独立缓存）
FROM mcr.azure.cn/azurelinux/base/nodejs:24 AS renderers
RUN tdnf install -y tar
WORKDIR "/app/FCloud3.AppFront/FCloud3Front"
COPY "./FCloud3.AppFront/FCloud3Front/public/renderers/collect-renderers.mjs" "./public/renderers/collect-renderers.mjs"
RUN node ./public/renderers/collect-renderers.mjs

# 构建vue前端
FROM mcr.azure.cn/azurelinux/base/nodejs:24 AS febuild
RUN tdnf install -y tar
RUN npm config set registry https://registry.npmmirror.com
RUN npm install -g pnpm
WORKDIR "/app/FCloud3.AppFront/FCloud3Front"
COPY "./FCloud3.AppFront/FCloud3Front/package.json" "./package.json"
COPY "./FCloud3.AppFront/FCloud3Front/pnpm-lock.yaml" "./pnpm-lock.yaml"
COPY "./FCloud3.AppFront/FCloud3Front/pnpm-workspace.yaml" "./pnpm-workspace.yaml"
RUN pnpm install --frozen-lockfile
COPY "./FCloud3.AppFront/FCloud3Front" "."
COPY --from=renderers /app/FCloud3.AppFront/FCloud3Front/public/renderers ./public/renderers
WORKDIR "/app/FCloud3.AppFront/FCloud3Plugins"
COPY "./FCloud3.AppFront/FCloud3Plugins" "."
RUN node buildPlugins.mjs
WORKDIR "/app/FCloud3.AppFront/FCloud3Front"
RUN npm run build-here

# 构建.net后端
FROM mcr.azure.cn/dotnet/sdk:10.0 AS bebuild
WORKDIR "/src"
COPY "./FCloud3.Sso/FCloud3.Sso.csproj" "./FCloud3.Sso/FCloud3.Sso.csproj"
COPY "./FCloud3.WikiPreprocessor/FCloud3.WikiPreprocessor.csproj" "./FCloud3.WikiPreprocessor/FCloud3.WikiPreprocessor.csproj"
COPY "./FCloud3.Diff/FCloud3.Diff.csproj" "./FCloud3.Diff/FCloud3.Diff.csproj"
COPY "./FCloud3.Entities/FCloud3.Entities.csproj" "./FCloud3.Entities/FCloud3.Entities.csproj"
COPY "./FCloud3.DbContexts/FCloud3.DbContexts.csproj" "./FCloud3.DbContexts/FCloud3.DbContexts.csproj"
COPY "./FCloud3.Repos/FCloud3.Repos.csproj" "./FCloud3.Repos/FCloud3.Repos.csproj"
COPY "./FCloud3.Services/FCloud3.Services.csproj" "./FCloud3.Services/FCloud3.Services.csproj"
COPY "./FCloud3.App/FCloud3.App.csproj" "./FCloud3.App/FCloud3.App.csproj"
WORKDIR "/src/FCloud3.App"
RUN dotnet restore
WORKDIR "/src"
COPY "./FCloud3.Sso" "./FCloud3.Sso"
COPY "./FCloud3.WikiPreprocessor" "./FCloud3.WikiPreprocessor"
COPY "./FCloud3.Diff" "./FCloud3.Diff"
COPY "./FCloud3.Entities" "./FCloud3.Entities"
COPY "./FCloud3.DbContexts" "./FCloud3.DbContexts"
COPY "./FCloud3.Repos" "./FCloud3.Repos"
COPY "./FCloud3.Services" "./FCloud3.Services"
COPY "./FCloud3.App" "./FCloud3.App"
RUN dotnet publish "./FCloud3.App/FCloud3.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 组合进.net运行环境
FROM mcr.azure.cn/dotnet/aspnet:10.0 AS final
# 设置系统时区为上海
ENV TZ=Asia/Shanghai
RUN ln -fs /usr/share/zoneinfo/$TZ /etc/localtime && dpkg-reconfigure -f noninteractive tzdata
# 绕过老版sqlserver协议问题 A connection was successfully established with the server, but then an error occurred during the pre-login handshake. (provider: SSL Provider, error: 31 - Encryption(ssl/tls) handshake failed)
# 如果不使用sqlserver或者使用的是新版sqlserver，可注释掉下面这行
RUN sed -i 's|\[openssl_init\]|&\nssl_conf = ssl_configuration\n[ssl_configuration]\nsystem_default = tls_system_default\n[tls_system_default]\nMinProtocol = TLSv1\nCipherString = DEFAULT@SECLEVEL=0|' /etc/ssl/openssl.cnf
WORKDIR "/app"
COPY --from=bebuild /app/publish .
COPY --from=febuild /app/FCloud3.AppFront/FCloud3Front/dist ./wwwroot
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "FCloud3.App.dll"]