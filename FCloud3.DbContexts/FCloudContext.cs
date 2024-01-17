﻿using FCloud3.Entities.Files;
using FCloud3.Entities.Identities;
using FCloud3.Entities.TextSection;
using FCloud3.Entities.Wiki;
using Microsoft.EntityFrameworkCore;

namespace FCloud3.DbContexts
{
    public class FCloudContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<WikiItem> WikiItems { get; set; }
        public DbSet<WikiPara> WikiParas { get; set; }
        public DbSet<TextSection> TextSections { get; set; }
        public DbSet<FileItem> FileItems { get; set; }
        public DbSet<FileDir> FileDirs { get; set; }
    }
}
