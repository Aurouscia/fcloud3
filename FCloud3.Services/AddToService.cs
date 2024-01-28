﻿using FCloud3.Services.Identities;
using FCloud3.Services.Wiki;
using FCloud3.Services.TextSec;
using Microsoft.Extensions.DependencyInjection;
using FCloud3.Services.Files;
using FCloud3.Services.Sys;
using Microsoft.Extensions.Configuration;
using FCloud3.Services.Files.Storage;
using FCloud3.Services.Files.Storage.Abstractions;

namespace FCloud3.Services
{
    public static class AddToService
    {
        public static IServiceCollection AddFCloudServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<UserService>();
            services.AddScoped<UserGroupService>();
            services.AddScoped<WikiItemService>();
            services.AddScoped<WikiParaService>();
            services.AddScoped<TextSectionService>();
            services.AddScoped<FileItemService>();
            services.AddScoped<FileDirService>();
            services.AddScoped<QuickSearchService>();

            string storageType = config["FileStorage:Type"] ?? "Local";
            if (storageType == "Local")
                services.AddSingleton<IStorage, LocalStorage>();
            else if (storageType == "Oss")
                services.AddSingleton<IStorage, OssStorage>();
            else
                throw new Exception("不支持的文件存储类型(配置项FileStorage:Type)");
            return services;
        }
    }
}
