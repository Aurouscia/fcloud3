using Aurouscia.FicKit.Currency.Database;
using FCloud3.App.Services.Utils;
using FCloud3.DbContexts;
using FCloud3.Entities.Files;
using FCloud3.Entities.Identities;
using FCloud3.Entities.Wiki;
using FCloud3.Services.Etc.TempData.Context;
using FCloud3.Services.Files;
using FCloud3.Services.Wiki;
using FCloud3.Services.WikiParsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCloud3.App.Controllers.Sys
{
    [Route("Init/{code}/[action]")]
    public class InitController:Controller
    {
        private readonly FCloudContext _context;
        private readonly TempDataContext _tempDataContext;
        private readonly CurrencyContext _currencyContext;
        private readonly WikiTitleContainService _wikiTitleContainService;
        private readonly FileDirService _fileDirService;
        private readonly WikiParsingService _wikiParsingService;
        private readonly static object migrateLockObj = new();
        public InitController(
            FCloudContext context,
            TempDataContext tempDataContext,
            CurrencyContext currencyContext,
            WikiTitleContainService wikiTitleContainService,
            FileDirService fileDirService,
            WikiParsingService wikiParsingService,
            IConfiguration config,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _tempDataContext = tempDataContext;
            _currencyContext = currencyContext;
            _wikiTitleContainService = wikiTitleContainService;
            _fileDirService = fileDirService;
            _wikiParsingService = wikiParsingService;

            var code = httpContextAccessor.HttpContext?.Request.RouteValues["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(code))
                throw new Exception("请输入配置文件中的MasterAdminCode");
            var trueCode = config["MasterAdminCode"] ?? throw new Exception("配置文件中MasterAdminCode未填");
            if (trueCode != code)
                throw new Exception("MasterAdminCode错误");
        }
        public IActionResult InitDb()
        {
            lock (migrateLockObj)
            {
                _context.Database.Migrate();
                _tempDataContext.Database.Migrate();
                _currencyContext.Database.Migrate();
                return this.ApiResp("已Migrate成功", true);
            }
        }
        public IActionResult InitWikis()
        {
            bool anyWikis = _context.WikiItems.Any();
            bool anyWikiParas = _context.WikiParas.Any();
            if(!anyWikis && !anyWikiParas)
            {
                WikiItem item = new WikiItem()
                {
                    Title = "测试词条1"
                };
                _context.WikiItems.Add(item);
                _context.SaveChanges();
                WikiPara p1 = new WikiPara()
                {
                    WikiItemId = item.Id,
                    Type = WikiParaType.Text,
                    Order = 0,
                };
                WikiPara p2 = new WikiPara()
                {
                    WikiItemId = item.Id,
                    Type = WikiParaType.Text,
                    Order = 1,
                };
                _context.WikiParas.AddRange(p1, p2);
                _context.SaveChanges();
            }
            return this.ApiResp();
        }
        public IActionResult InitUser()
        {
            var initUserName = "admin";
            var initPassword = "fcloud987123";
            bool anyUsersNamedThis = _context.Users.Any(x=>x.Name == initUserName);
            if (!anyUsersNamedThis)
            {
                User u1 = new User()
                {
                    Name = initUserName,
                    Type = UserType.SuperAdmin,
                    PwdEncrypted = new UserPwdEncryption().Run(initPassword)
                };
                _context.Users.AddRange(u1);
                _context.SaveChanges();
            }
            return this.ApiResp();
        }
        public IActionResult InitFileDirs()
        {
            bool anyFileDirs = _context.FileDirs.Any();
            if (!anyFileDirs)
            {
                FileDir d1 = new FileDir()
                {
                    Name = "一号文件夹",
                };
                FileDir d2 = new FileDir()
                {
                    Name = "二号文件夹",
                };
                _context.FileDirs.AddRange(d1, d2);
                _context.SaveChanges();
            }
            return this.ApiResp();
        }

        /// <summary>
        /// 【⚠警告】该操作对内存和数据库均有极大负担，请谨慎操作
        /// </summary>
        /// <returns></returns>
        public IActionResult WikiTitleContainAutoFill()
        {
            var existingContainsForText = _context.WikiTitleContains
                .Where(x => x.Type == WikiTitleContainType.TextSection)
                .Select(x => x.ObjectId);
            var existingContainsForTable = _context.WikiTitleContains
                .Where(x => x.Type == WikiTitleContainType.FreeTable)
                .Select(x => x.ObjectId);

            var allTexts = _context.TextSections
                .Where(x => !x.Deleted)
                .Where(x => x.Content != null && x.Content.Length < 20000)
                .Where(x => !existingContainsForText.Contains(x.Id))
                .Select(x => new { x.Id }).ToList();
            var allTables = _context.FreeTables
                .Where(x => !x.Deleted)
                .Where(x => x.Data != null && x.Data.Length < 20000)
                .Where(x => !existingContainsForTable.Contains(x.Id))
                .Select(x => new { x.Id }).ToList();

            List<WikiTitleContain> newContains = [];
            allTexts.ForEach(x =>
            {
                try
                {
                    var res = _wikiTitleContainService.AutoFill(x.Id, WikiTitleContainType.TextSection);
                    res.Items.ForEach(r =>
                    {
                        newContains.Add(new()
                        {
                            ObjectId = x.Id,
                            Type = WikiTitleContainType.TextSection,
                            WikiId = r.Id
                        });
                    });

                    if (newContains.Count > 100)
                    {
                        _context.WikiTitleContains.AddRange(newContains);
                        _context.SaveChanges();
                        newContains.Clear();
                    }
                }
                catch { }
            });
            allTables.ForEach(x =>
            {
                try
                {
                    var res = _wikiTitleContainService.AutoFill(x.Id, WikiTitleContainType.FreeTable);
                    res.Items.ForEach(r =>
                    {
                        newContains.Add(new()
                        {
                            ObjectId = x.Id,
                            Type = WikiTitleContainType.FreeTable,
                            WikiId = r.Id
                        });
                    });

                    if (newContains.Count > 100)
                    {
                        _context.WikiTitleContains.AddRange(newContains);
                        _context.SaveChanges();
                        newContains.Clear();
                    }
                }
                catch { }
            });
            _context.WikiTitleContains.AddRange(newContains);
            _context.SaveChanges();
            return this.ApiResp("已完成", true);
        }

        public IActionResult FileDirSystemFix()
        {
            _fileDirService.ManualFixInfoForAll(out var errmsg);
            if (errmsg is not null)
                return this.ApiFailedResp(errmsg);
            return this.ApiResp("已完成");
        }
        public IActionResult FileDirSystemLoopFix()
        {
            var foundIds = _fileDirService.ManualLoopFix();
            if (foundIds.Count == 0)
                return this.ApiResp("未发现循环结构");
            return this.ApiResp("已发现并修复循环结构：" + string.Join(", ", foundIds));
        }

        public IActionResult NameLengthFix()
        {
            //蠢事：设置数据库内名称长度限制和新增列放在了同一个migration进行
            //试图migrate时报错：会造成数据截断，拒绝执行
            //试图更新名称过长的模型时：缺少列，无法ToList
            //只能用ExecuteUpdate修复

            //忘记设置数据库长度就算了，别管他了
            var titleTooLongWikis = _context.WikiItems.Where(x => x.Title != null && x.Title.Length > WikiItem.titleMaxLength).ToList();
            titleTooLongWikis.ForEach(w => w.Title = w.Title!.Substring(0, 32));
            var nameTooLongDirs = _context.FileDirs
                .Where(x => x.Name != null && x.Name.Length > FileDir.nameMaxLength)
                .Select(x => new {x.Id, x.Name})
                .ToList();
            foreach(var dir in nameTooLongDirs)
            {
                _context.FileDirs.Where(x => x.Id == dir.Id)
                    .ExecuteUpdate(c => c.SetProperty(d => d.Name, dir.Name!.Substring(0, FileDir.nameMaxLength)));
            }
            _context.SaveChanges();
            return this.ApiResp("已完成");
        }

        public IActionResult RmDupGroupRelations()
        {
            var relations = _context.UserToGroups.ToList();
            var distincted =
                relations.DistinctBy(x => $"{x.GroupId}-{x.UserId}").ToList();
            var needRemove = relations.Except(distincted).ToList();
            var existed = needRemove.All(x =>
                distincted.Any(r => x.UserId == r.UserId && x.GroupId == r.GroupId));
            if (existed)
            {
                _context.RemoveRange(needRemove);
                _context.SaveChanges();
                return this.ApiResp();
            }
            return this.ApiFailedResp("失败");
        }

        public IActionResult GroupGC()
        {
            var groupsWithAnyRelation = _context.UserToGroups
                .Select(x => x.GroupId)
                .Distinct();
            var emptyGroups = _context.UserGroups
                .Where(x => !groupsWithAnyRelation.Contains(x.Id))
                .ToList();
            if (emptyGroups.Count == 0)
                return this.ApiResp("没有需要清理的空用户组");
            _context.UserGroups.RemoveRange(emptyGroups);
            _context.SaveChanges();
            return this.ApiResp($"已清理 {emptyGroups.Count} 个空用户组");
        }

        public IActionResult SetUpdateToLastActive()
        {
            var count = _context.WikiItems.ExecuteUpdate(spc => spc.SetProperty(w => w.LastActive, w => w.Updated));
            return this.ApiResp($"OK：{count}");
        }

        public IActionResult EnforceParseAll()
        {
            //解析所有词条，以创建Ref
            var existingWikisIds = _context.WikiItems
                .Where(x => !x.Deleted)
                .Select(x => x.Id).ToList();
            var originalRefCount = _context.WikiRefs.Count();
            existingWikisIds.ForEach(wikiId =>
            {
                _ = _wikiParsingService.GetParsedWiki(wikiId);
            });
            var newRefCount = _context.WikiRefs.Count();
            return Ok($"Ref {originalRefCount} -> {newRefCount}");
        }
    }
}
