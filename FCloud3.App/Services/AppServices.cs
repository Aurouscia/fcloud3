﻿using FCloud3.App.Services.Filters;
using FCloud3.App.Services.Utils;
using FCloud3.App.Utils;
using FCloud3.HtmlGen.Util;
using FCloud3.Repos;
using FCloud3.Services;
using FCloud3.Services.Files.Storage.Abstractions;
using FCloud3.Services.Identities;
using Microsoft.AspNetCore.Authorization;
using Serilog;

namespace FCloud3.App.Services
{
    public static class AppServices
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<HttpUserIdProvider>();
            services.AddScoped<HttpUserInfoService>();
            services.AddControllers(options => {
                options.Filters.Add<ApiExceptionFilter>();
            });
            services.AddAuthGrantedActionFilter();
            services.AddFilePathBaseConstraint();
            services.AddMemoryCache();
            services.AddSingleton<ILocatorHash, LocatorHash>();
            services.AddScoped<WikiParserProviderService>();
            services.AddScoped<ICommitingUserIdProvider, HttpUserIdProvider>();
            services.AddScoped<IOperatingUserIdProvider, HttpUserIdProvider>();
            services.AddSingleton<IFileStreamHasher, FileStreamHasher>();
            services.AddSingleton<IUserPwdEncryption, UserPwdEncryption>();
            return services;
        }
    }
}
