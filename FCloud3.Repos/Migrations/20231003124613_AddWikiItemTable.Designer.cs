﻿// <auto-generated />
using System;
using FCloud3.Repos.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FCloud3.Repos.Migrations
{
    [DbContext(typeof(FCloudContext))]
    [Migration("20231003124613_AddWikiItemTable")]
    partial class AddWikiItemTable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("FCloud3.Repos.Models.Corr.Corr", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("A")
                        .HasColumnType("int");

                    b.Property<int>("B")
                        .HasColumnType("int");

                    b.Property<int>("CorrType")
                        .HasColumnType("int");

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreatorUserId")
                        .HasColumnType("int");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<int>("Order")
                        .HasColumnType("int");

                    b.Property<DateTime>("Updated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("Corrs");
                });

            modelBuilder.Entity("FCloud3.Repos.Models.Identities.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("AvatarFileName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreatorUserId")
                        .HasColumnType("int");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PwdMd5")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Updated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("FCloud3.Repos.Models.Wiki.WikiItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreatorUserId")
                        .HasColumnType("int");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<int>("OwnerUserId")
                        .HasColumnType("int");

                    b.Property<string>("Title")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Updated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("WikiItems");
                });

            modelBuilder.Entity("FCloud3.Repos.Models.Wiki.WikiTextPara", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Content")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContentBrief")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreatorUserId")
                        .HasColumnType("int");

                    b.Property<bool>("Deleted")
                        .HasColumnType("bit");

                    b.Property<string>("Title")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("Updated")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("WikiTextParas");
                });
#pragma warning restore 612, 618
        }
    }
}
