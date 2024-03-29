﻿using FCloud3.DbContexts;
using FCloud3.Entities.Files;
using FCloud3.Entities.Wiki;
using FCloud3.Repos;
using FCloud3.Repos.Files;
using FCloud3.Repos.Identities;
using FCloud3.Repos.Wiki;
using FCloud3.Services.Files.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FCloud3.Services.Files
{
    public class FileDirService
    {
        private readonly UserRepo _userRepo;
        private readonly int _userId;
        private readonly FileDirRepo _fileDirRepo;
        private readonly FileItemRepo _fileItemRepo;
        private readonly WikiItemRepo _wikiItemRepo;
        private readonly WikiToDirRepo _wikiToDirRepo;
        private readonly DbTransactionService _transaction;
        private readonly IStorage _storage;

        public FileDirService(
            UserRepo userRepo,
            IOperatingUserIdProvider userIdProvider,
            FileDirRepo fileDirRepo,
            FileItemRepo fileItemRepo,
            WikiItemRepo wikiItemRepo,
            WikiToDirRepo wikiToDirRepo,
            DbTransactionService transaction,
            IStorage storage)
        {
            _userRepo = userRepo;
            _userId = userIdProvider.Get();
            _fileDirRepo = fileDirRepo;
            _fileItemRepo = fileItemRepo;
            _wikiItemRepo = wikiItemRepo;
            _wikiToDirRepo = wikiToDirRepo;
            _transaction = transaction;
            _storage = storage;
        }

        public FileDir? GetById(int id)
        {
            return _fileDirRepo.GetById(id);
        }

        public string[]? GetPathById(int id)
        {
            return _fileDirRepo.GetPathById(id);
        }

        public FileDirIndexResult? GetContent(IndexQuery q, string[] path, out string? errmsg)
        {
            if (path.Length == 1 || path.Length == 2)
            {
                if (path[0] == homelessItems)
                {
                    return GetHomelessItems(q, path, out errmsg);
                }
            }
            errmsg = null;
            var chain = _fileDirRepo.GetChainByPath(path);
            if(chain is null) {
                errmsg = "找不到指定路径的文件夹";
                return null;
            }
            List<string> friendlyPath = chain.ConvertAll(x => x.Name??"??").ToList();
            var thisDirId = 0;
            if(chain.Count>0)
                thisDirId = chain.Last().Id;
            var subDirsQ = _fileDirRepo.GetChildrenById(thisDirId);
            if(subDirsQ is null)
                return null;
            var subDirs = _fileDirRepo.IndexFilterOrder(subDirsQ,q);
            IndexResult<FileDirIndexResult.FileDirSubDir> subDirsData = subDirs.TakePage(q,x => new FileDirIndexResult.FileDirSubDir()
            {
                Id = x.Id,
                Name = x.Name,
                UrlPathName = x.UrlPathName,
                Updated = x.Updated.ToString("yy/MM/dd HH:mm"),
                ByteCount = x.ByteCount,
            });


            //三个查询共用一个IndexQuery，查询前翻译一下名字
            static string keyReplaceForFileItem(string k)
            {
                if (k == nameof(FileDir.Name))
                    return nameof(FileItem.DisplayName);
                return k;
            }
            IndexResult<FileDirIndexResult.FileDirItem>? itemsData = null;
            if (thisDirId != 0)
            {
                var itemsQ = _fileItemRepo.GetByDirId(thisDirId);
                var items = _fileItemRepo.IndexFilterOrder(itemsQ, q, keyReplaceForFileItem);

                itemsData = items.TakePage(q, x => new FileDirIndexResult.FileDirItem()
                {
                    Id = x.Id,
                    Name = x.DisplayName,
                    OwnerName = "",
                    ByteCount = x.ByteCount,
                    Updated = x.Updated.ToString("yy/MM/dd HH:mm"),
                    Url = _storage.FullUrl(x.StorePathName ?? "missing")
                });
            }

            static string keyReplaceForWikiItem(string k)
            {
                if (k == nameof(FileDir.Name))
                    return nameof(WikiItem.Title);
                return k;
            }
            IndexResult<FileDirIndexResult.FileDirWiki>? wikisData = null;
            if (thisDirId != 0)
            {
                var wikisQ = from wiki in _wikiItemRepo.Existing
                             from relation in _wikiToDirRepo.Existing
                             where relation.DirId == thisDirId
                             where wiki.Id == relation.WikiId
                             select wiki;
                var wikis = _wikiItemRepo.IndexFilterOrder(wikisQ, q, keyReplaceForWikiItem);

                wikisData = wikis.TakePage(q, x => new FileDirIndexResult.FileDirWiki()
                {
                    Id = x.Id,
                    Name = x.Title,
                    UrlPathName = x.UrlPathName,
                    OwnerName = "",
                    Updated = x.Updated.ToString("yy/MM/dd HH:mm"),
                });
            }
            return new() { 
                Items = itemsData,
                SubDirs = subDirsData,
                Wikis = wikisData,
                ThisDirId = thisDirId,
                FriendlyPath = friendlyPath
            };
        }
        public FileDirIndexResult? GetHomelessItems(IndexQuery q, string[] path, out string? errmsg)
        {
            string? userName = null;
            if (path.Length == 0 || path.Length > 2 || path[0] != homelessItems)
            {
                errmsg = "内部错误：GetHomelessItems错误调用";
                return null;
            }
            if (path.Length == 2)
                userName = path[1];
            var userId = 0;
            if (!string.IsNullOrEmpty(userName)) 
            {
                userId = _userRepo.GetByName(userName).Select(x => x.Id).FirstOrDefault();
            }
            if (userId == 0) 
            {
                userId = _userId;
                userName = _userRepo.Existing.Where(x => x.Id == userId).Select(x => x.Name).FirstOrDefault();
            }

            var homelessFiles =
                from f in _fileItemRepo.Existing
                where f.InDir == 0
                where f.CreatorUserId == userId
                select f;
            static string keyReplaceForFileItem(string k)
            {
                if (k == nameof(FileDir.Name))
                    return nameof(FileItem.DisplayName);
                return k;
            }
            var items = _fileItemRepo.IndexFilterOrder(homelessFiles, q, keyReplaceForFileItem);
            IndexResult<FileDirIndexResult.FileDirItem>? itemsData = null;
            itemsData = items.TakePage(q, x => new FileDirIndexResult.FileDirItem()
            {
                Id = x.Id,
                Name = x.DisplayName,
                OwnerName = "",
                ByteCount = x.ByteCount,
                Updated = x.Updated.ToString("yy/MM/dd HH:mm"),
                Url = _storage.FullUrl(x.StorePathName ?? "missing")
            });

            FileDirIndexResult res = new()
            {
                Wikis = new(new(),1,1,1),
                Items = itemsData,
                SubDirs = new(new(),1,1,1),
                FriendlyPath = new List<string> { $"无归属文件(属于 {userName})" },
                ThisDirId = -1
            };
            errmsg = null;
            return res;
        }

        public bool UpdateInfo(int id, string? name, string? urlPathName, out string? errmsg)
        {
            var target = _fileDirRepo.GetById(id);
            if (target is null)
            {
                errmsg = "找不到该文件夹";
                return false;
            }
            if(string.IsNullOrEmpty(urlPathName))
            {
                errmsg = "文件夹路径名不能为空";
                return false;
            }
            if (PreservedUrlPathNames().Contains(urlPathName))
            {
                errmsg = $"请勿使用保留文件夹名[{urlPathName}]";
                return false;
            }

            target.Name = name;
            target.UrlPathName = urlPathName;

            if(!_fileDirRepo.TryEdit(target, out errmsg))
                return false;
            return true;
        }

        public List<int>? MoveFilesIn(int distDirId, List<int> fileItemIds,out string? failMsg, out string? errmsg)
        {
            failMsg = null;
            //TODO权限验证，文件是不是自己的，文件夹有没有放文件权限

            //fileItemIds = fileItemIds;//过滤掉没有权限的
            
            if (distDirId < 0)
            {
                errmsg = "请刷新后重试(未找到指定路径的文件夹)";
                return null;
            }
            if(distDirId == 0)
            {
                errmsg = "不能将文件放到根目录";
                return null;
            }
            if (!_fileItemRepo.SetInDirForRange(distDirId, fileItemIds, out errmsg))
                return null;

            errmsg = null;
            return fileItemIds;
        }
        public List<int>? MoveFilesIn(string[] dirPath, List<int> fileItemIds, out string? failMsg, out string? errmsg)
        {
            int dirId = _fileDirRepo.GetIdByPath(dirPath);
            return MoveFilesIn(dirId, fileItemIds, out failMsg, out errmsg);
        }
        public bool MoveFileIn(string[] dirPath, int fileItemId, out string? errmsg)
        {
            var list = new List<int>() { fileItemId };
            _ = MoveFilesIn(dirPath, list ,out string? failMsg, out errmsg);
            if (errmsg is null && failMsg is not null)
                errmsg = failMsg;
            if (errmsg is not null)
                return false;
            return true;
        }

        public List<int>? MoveDirsIn(int distDirId, List<int> fileDirIds, out string? failMsg, out string? errmsg)
        {
            failMsg = null;
            var dist = _fileDirRepo.GetById(distDirId);
            if (dist is null && distDirId!=0)
            {
                errmsg = "未找到目的地文件夹";
                return null; 
            }
            //TODO权限验证，文件夹是不是自己的，文件夹有没有放文件权限
            //fileDirIds = fileDirIds 过滤掉没权限的，并写入failMsg
            errmsg = null;
            var ds = _fileDirRepo.GetRangeByIds(fileDirIds).ToList();

            var setDepth = dist is null ? 0 : dist.Depth+1;

            ds.ForEach(x => {
                x.Depth = setDepth;
                x.ParentDir = distDirId;});
            if (!_fileDirRepo.TryEditRange(ds, out errmsg))
                return null;
            if (!_fileDirRepo.UpdateDescendantsInfoFor(ds, out errmsg))
                return null;
            
            return fileDirIds;
        }
        public List<int>? MoveDirsIn(string[] dirPath, List<int> fileDirIds, out string? failMsg, out string? errmsg)
        {
            int distDirId = _fileDirRepo.GetIdByPath(dirPath);
            if (distDirId < 0)
            {
                failMsg = "操作失败";
                errmsg = "请刷新后重试(未找到指定路径的文件夹)";
                return null;
            }
            return MoveDirsIn(distDirId, fileDirIds,out failMsg, out errmsg);
        }
        public List<int>? MoveWikisIn(int distDirId, List<int>wikiItemIds,out string? failMsg, out string? errmsg)
        {
            failMsg = null;
            //TODO 身份验证什么的，过滤没有权限的
            if (!_wikiToDirRepo.AddWikisToDir(wikiItemIds, distDirId, out errmsg))
                return null;
            return wikiItemIds;
        }

        public FileDirPutInResult? MoveThingsIn(string[] dirPath, List<int>? fileItemIds, List<int>? fileDirIds, List<int>? wikiItemIds, out string? errmsg)
        {
            errmsg = null;
            string? failMsg = null;
            bool didSth = false;
            List<int>? fileItemSuccess = null;
            List<int>? fileDirSuccess = null;
            List<int>? wikiItemSuccess = null;

            List<int>? chain = _fileDirRepo.GetChainIdsByPath(dirPath);
            if(chain is null) { errmsg = "找不到指定路径的文件夹"; return null; }

            int distDirId = chain.Count>0 ? chain.Last() : 0;

            if (fileDirIds is not null && fileDirIds.Count > 0)
            {
                if (fileDirIds.Any(x => chain.Contains(x)))
                {
                    errmsg = "检测到循环，请勿将文件夹移入自身或子级";
                    return null;
                }
                fileDirSuccess = MoveDirsIn(distDirId, fileDirIds, out failMsg, out errmsg);
                didSth = true;
            }
            if (fileItemIds is not null && fileItemIds.Count > 0)
            {
                fileItemSuccess = MoveFilesIn(distDirId, fileItemIds, out failMsg, out errmsg);
                didSth = true;
            }
            if (wikiItemIds is not null && wikiItemIds.Count > 0)
            {
                wikiItemSuccess = MoveWikisIn(distDirId, wikiItemIds, out failMsg, out errmsg);
                didSth = true;
            }
            if (!didSth)
            {
                errmsg = "未选择任何要移入的对象";
                return null;
            }
            var resp = new FileDirPutInResult()
            {
                FileItemSuccess = fileItemSuccess,
                FileDirSuccess = fileDirSuccess,
                WikiItemSuccess = wikiItemSuccess,

                FailMsg = failMsg,
            };
            return resp;
        }

        public bool Create(int parentDir, string? name, string? urlPathName, out string? errmsg)
        {
            //TODO验证在父文件夹有没有权限
            FileDir? parent = _fileDirRepo.GetById(parentDir);
            int depth = 0;
            if (parent is not null)
                depth = parent.Depth + 1;
            FileDir newDir = new()
            {
                ParentDir = parentDir,
                Name = name,
                UrlPathName = urlPathName,
                Depth = depth
            };
            return _fileDirRepo.TryAdd(newDir, out errmsg);
        }
        public bool Delete(int dirId,out string? errmsg)
        {
            errmsg = null;
            var item = _fileDirRepo.GetById(dirId);
            if (item is null)
                return false;
            var items = _fileItemRepo.GetByDirId(dirId).Count();
            if (items > 0) 
            {
                errmsg = "只能删除空文件夹";
                return false;
            }
            var wikis = _wikiToDirRepo.GetDirWikiIds(dirId).Count;
            if (wikis > 0)
            {
                errmsg = "只能删除空文件夹";
                return false;
            }
            var subDirs = _fileDirRepo.GetChildrenById(dirId)?.Count() ?? 0;
            if (subDirs > 0)
            {
                errmsg = "只能删除空文件夹";
                return false;
            }
            return _fileDirRepo.TryRemove(item,out errmsg);
        }


        private const string homelessItems = "homeless-items";
        private static List<string> PreservedUrlPathNames()
        {
            return new List<string> { homelessItems };
        } 
    }

    public class FileDirIndexResult
    {
        public IndexResult<FileDirSubDir>? SubDirs { get; set; }
        public IndexResult<FileDirItem>? Items { get; set; }
        public IndexResult<FileDirWiki>? Wikis { get; set; }
        public int ThisDirId { get; set; }
        public List<string>? FriendlyPath { get; set; }

        public class FileDirSubDir
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? UrlPathName { get; set; }
            public string? Updated { get; set; }
            public string? OwnerName { get; set; }
            public int ByteCount { get; set; }
            public int FileNumber { get; set; }
        }
        public class FileDirItem
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Updated { get; set; }
            public string? OwnerName { get; set; }
            public int ByteCount { get; set; }
            public string? Url { get; set; }
        }
        public class FileDirWiki
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? UrlPathName { get; set; }
            public string? Updated { get; set; }
            public string? OwnerName { get; set; }
        }
    }
    public class FileDirPutInResult
    {
        public List<int>? FileItemSuccess { get; set; }
        public List<int>? FileDirSuccess { get; set; }
        public List<int>? WikiItemSuccess { get; set; }
        public string? FailMsg { get; set; }
    }
}
