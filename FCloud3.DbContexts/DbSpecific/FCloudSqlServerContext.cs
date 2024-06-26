﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.DbContexts.DbSpecific
{
    public class FCloudSqlServerContext : FCloudContext
    {
        private readonly FCloudContextOptions _options;
        private const string acceptDbType = "sqlserver";
        public FCloudSqlServerContext(FCloudContextOptions options)
        {
            if (options.Type?.ToLower() != acceptDbType)
                throw new Exception($"数据库类型配置异常，应为:{acceptDbType}");
            _options = options;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            Action<SqlServerDbContextOptionsBuilder> action = x => { };
            if (_options.SqlServer is not null && _options.SqlServer.CompatibilityLevel > 0)
                action = x => x.UseCompatibilityLevel(_options.SqlServer.CompatibilityLevel);

            optionsBuilder.UseSqlServer(_options.ConnStr, action);
        }
    }
}
