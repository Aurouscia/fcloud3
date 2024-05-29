﻿using FCloud3.DbContexts;
using FCloud3.Entities.Files;
using FCloud3.Entities.Identities;
using FCloud3.Entities.Messages;
using FCloud3.Entities.Wiki;
using FCloud3.Repos;
using FCloud3.Repos.Files;
using FCloud3.Repos.Identities;
using FCloud3.Repos.Messages;
using FCloud3.Repos.Wiki;
using FCloud3.Services.Etc.Metadata;
using FCloud3.Services.Files.Storage.Abstractions;
using FCloud3.Services.Identities;
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
        private readonly OpRecordRepo _opRecordRepo;
        private readonly AuthGrantService _authGrantService;
        private readonly UserMetadataService _userMetadataService;
        private readonly IStorage _storage;

        public FileDirService(
            UserRepo userRepo,
            IOperatingUserIdProvider userIdProvider,
            FileDirRepo fileDirRepo,
            FileItemRepo fileItemRepo,
            WikiItemRepo wikiItemRepo,
            WikiToDirRepo wikiToDirRepo,
            OpRecordRepo opRecordRepo,
            AuthGrantService authGrantService,
            UserMetadataService userMetadataService,
            IStorage storage)
        {
            _userRepo = userRepo;
            _userId = userIdProvider.Get();
            _fileDirRepo = fileDirRepo;
            _fileItemRepo = fileItemRepo;
            _wikiItemRepo = wikiItemRepo;
            _wikiToDirRepo = wikiToDirRepo;
            _opRecordRepo = opRecordRepo;
            _authGrantService = authGrantService;
            _userMetadataService = userMetadataService;
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


        private enum FileDirContentItemType
        {
            Dir, WikiItem, FileItem
        }
        private struct FileDirContentItem(int id, FileDirContentItemType type)
        {
            public int Id { get; } = id;
            public FileDirContentItemType Type { get; } = type;
        }
        public FileDirIndexResult? GetContent(IndexQuery q, string[] path, out string? errmsg)
        {
            q.SelfCheck();
            if (path.Length == 1 || path.Length == 2)
            {
                if (path[0] == homelessItems)
                {
                    return GetHomelessItems(q, path, out errmsg);
                }
            }
            errmsg = null;
            var chain = _fileDirRepo.GetChainByPath(path);
            if (chain is null) {
                errmsg = "找不到指定路径的文件夹";
                return null;
            }
            List<string> friendlyPath = chain.ConvertAll(x => x.Name ?? "??").ToList();
            var thisDirId = 0;
            if (chain.Count > 0)
                thisDirId = chain.Last().Id;

            var ownerId = _fileDirRepo.GetOwnerIdById(thisDirId);
            var ownerName = "";
            if (ownerId > 0)
                ownerName = _userMetadataService.Get(ownerId)?.Name ?? "??";


            List<FileDirContentItem> contents = [];
            static string keyReplaceForWikiItem(string k)
            {
                if (k == nameof(FileDir.Name))
                    return nameof(WikiItem.Title);
                return k;
            }
            static string keyReplaceForFileItem(string k)
            {
                if (k == nameof(FileDir.Name))
                    return nameof(FileItem.DisplayName);
                return k;
            }


            var subDirsQ = _fileDirRepo.GetChildrenById(thisDirId);
            if (subDirsQ is null)
                return null;
            subDirsQ = _fileDirRepo.IndexFilterOrder(subDirsQ, q);
            contents.AddRange(subDirsQ.Select(x => new FileDirContentItem(x.Id, FileDirContentItemType.Dir)).ToList());

            if (thisDirId > 0)
            {
                var wikisQ = from wiki in _wikiItemRepo.Existing
                             from relation in _wikiToDirRepo.Existing
                             where relation.DirId == thisDirId
                             where wiki.Id == relation.WikiId
                             select wiki;
                wikisQ = _wikiItemRepo.IndexFilterOrder(wikisQ, q, keyReplaceForWikiItem);
                contents.AddRange(wikisQ.Select(x => new FileDirContentItem(x.Id, FileDirContentItemType.WikiItem)).ToList());
            

                var filesQ = _fileItemRepo.GetByDirId(thisDirId);
                filesQ = _fileItemRepo.IndexFilterOrder(filesQ, q, keyReplaceForFileItem);
                contents.AddRange(filesQ.Select(x => new FileDirContentItem(x.Id, FileDirContentItemType.FileItem)).ToList());
            }
            var itemsPaged = contents.AsQueryable().TakePage(q, out int totalCount, out int pageIdx, out int pageCount).ToList();

            var subDirList = _fileDirRepo.GetRangeByIdsOrdered(
                ids: itemsPaged.Where(x => x.Type == FileDirContentItemType.Dir).Select(x => x.Id),
                converter: FileDirIndexResult.FileDirSubDir.Converter);
            var wikiList = _wikiItemRepo.GetRangeByIdsOrdered(
                ids: itemsPaged.Where(x => x.Type == FileDirContentItemType.WikiItem).Select(x => x.Id),
                converter: FileDirIndexResult.FileDirWiki.Converter);
            var fileList = _fileItemRepo.GetRangeByIdsOrdered(
                ids: itemsPaged.Where(x => x.Type == FileDirContentItemType.FileItem).Select(x => x.Id),
                converter: x => FileDirIndexResult.FileDirItem.Converter(x, _storage.FullUrl));

            var subDirData = new IndexResult<FileDirIndexResult.FileDirSubDir>(subDirList, 0, 0, 0);
            var wikiData = new IndexResult<FileDirIndexResult.FileDirWiki>(wikiList, 0, 0, 0);
            var fileData = new IndexResult<FileDirIndexResult.FileDirItem>(fileList, 0, 0, 0);

            //subDirsData会被用来显示页数
            subDirData.PageCount = pageCount;
            subDirData.TotalCount = totalCount;
            subDirData.PageIdx = pageIdx;

            return new() { 
                Items = fileData,
                SubDirs = subDirData,
                Wikis = wikiData,
                ThisDirId = thisDirId,
                OwnerId = ownerId,
                OwnerName = ownerName,
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
            itemsData = items.TakePageAndConvertOneByOne(q, x => 
                new FileDirIndexResult.FileDirItem(x.Id,x.DisplayName,x.Updated,x.ByteCount, _storage.FullUrl(x.StorePathName ?? "??"))
            );

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

            string record = "";
            if (target.Name != name)
                record += $"将 {target.Name} 更名为 {name} ; ";
            if (target.UrlPathName != urlPathName)
                record += $"将路径名 {target.UrlPathName} 改为 {urlPathName}";

            target.Name = name;
            target.UrlPathName = urlPathName;

            if(!_fileDirRepo.TryEdit(target, out errmsg))
                return false;

            if (!string.IsNullOrEmpty(record))
                _opRecordRepo.Record(OpRecordOpType.Edit, OpRecordTargetType.FileDir, record);
            return true;
        }

        public List<int>? MoveFilesIn(int distDirId, List<int> fileItemIds,out string? failMsg, out string? errmsg)
        {
            failMsg = null;
            if (fileItemIds.Count > 5)
            {
                errmsg = "太多了，请一个个移";
                return null;
            }
            int originalCount = fileItemIds.Count;
            fileItemIds.RemoveAll(x => !_authGrantService.Test(AuthGrantOn.FileItem, x));
            if (originalCount > fileItemIds.Count)
                failMsg = "无权移动该文件";
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

            if (fileItemIds.Count >= 1)
            {
                if (!_fileItemRepo.SetInDirForRange(distDirId, fileItemIds, out errmsg))
                    return null;
            }

            errmsg = null;
            return fileItemIds;
        }
        public bool MoveFileIn(int distDirId, int fileItemId, out string? errmsg)
        {
            if(!_authGrantService.Test(AuthGrantOn.FileItem, fileItemId))
            {
                errmsg = "无权移动该文件";
                return false;
            }
            var list = new List<int>() { fileItemId };
            _ = MoveFilesIn(distDirId, list, out string? failMsg, out errmsg);
            if (errmsg is null && failMsg is not null)
                errmsg = failMsg;
            if (errmsg is not null)
                return false;
            _fileDirRepo.SetUpdateTimeAncestrally(distDirId, out _);
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

            if (fileDirIds.Count > 5)
            {
                errmsg = "太多了，请一个个移";
                return null;
            }
            int originalCount = fileDirIds.Count;
            fileDirIds.RemoveAll(x => !_authGrantService.Test(AuthGrantOn.Dir, x));
            if (originalCount > fileDirIds.Count)
                failMsg = "无权移动该目录";

            if (fileDirIds.Count == 0)
            {
                errmsg = null;
                return null;
            }

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
        public List<int>? MoveWikisIn(int distDirId, List<int> wikiItemIds,out string? failMsg, out string? errmsg)
        {
            failMsg = null;
            if (wikiItemIds.Count > 5)
            {
                errmsg = "太多了，请一个个移";
                return null;
            }
            int originalCount = wikiItemIds.Count;
            wikiItemIds.RemoveAll(x => !_authGrantService.Test(AuthGrantOn.WikiItem, x));
            if (originalCount > wikiItemIds.Count)
                failMsg = "无权移动该词条";
            if (wikiItemIds.Count > 0)
            {
                if (!_wikiToDirRepo.AddWikisToDir(wikiItemIds, distDirId, out errmsg))
                    return null;
            }
            errmsg = null;
            return wikiItemIds;
        }


        public FileDirPutInResult? MoveThingsIn(int dirId, List<int>? fileItemIds, List<int>? fileDirIds, List<int>? wikiItemIds, out string? errmsg)
        {
            List<int>? chain = _fileDirRepo.GetChainIdsById(dirId);
            return MoveThingsIn(chain, fileItemIds, fileDirIds, wikiItemIds, out errmsg);
        }
        public FileDirPutInResult? MoveThingsIn(string[] dirPath, List<int>? fileItemIds, List<int>? fileDirIds, List<int>? wikiItemIds, out string? errmsg)
        {
            List<int>? chain = _fileDirRepo.GetChainIdsByPath(dirPath);
            return MoveThingsIn(chain, fileItemIds,fileDirIds, wikiItemIds,out errmsg);
        }
        private FileDirPutInResult? MoveThingsIn(List<int>? dirIdsChain, List<int>? fileItemIds, List<int>? fileDirIds, List<int>? wikiItemIds, out string? errmsg)
        {
            errmsg = null;
            string? failMsg = null;
            bool didSth = false;
            List<int>? fileItemSuccess = null;
            List<int>? fileDirSuccess = null;
            List<int>? wikiItemSuccess = null;

            if (dirIdsChain is null) { errmsg = "找不到指定路径的文件夹"; return null; }

            int distDirId = dirIdsChain.Count > 0 ? dirIdsChain.Last() : 0;

            if (fileDirIds is not null && fileDirIds.Count > 0)
            {
                if (fileDirIds.Any(x => dirIdsChain.Contains(x)))
                {
                    errmsg = "检测到循环，请勿将文件夹移入自身或子级";
                    return null;
                }
                fileDirSuccess = MoveDirsIn(distDirId, fileDirIds, out failMsg, out errmsg);
                if (errmsg is not null)
                    return null;
                didSth = true;
            }
            if (fileItemIds is not null && fileItemIds.Count > 0)
            {
                fileItemSuccess = MoveFilesIn(distDirId, fileItemIds, out failMsg, out errmsg);
                if (errmsg is not null)
                    return null;
                didSth = true;
            }
            if (wikiItemIds is not null && wikiItemIds.Count > 0)
            {
                wikiItemSuccess = MoveWikisIn(distDirId, wikiItemIds, out failMsg, out errmsg);
                if (errmsg is not null)
                    return null;
                didSth = true;
            }
            if (!didSth)
            {
                errmsg = "未选择任何要移入的对象";
                return null;
            }
            
            _fileDirRepo.SetUpdateTime(dirIdsChain);
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
            if(_fileDirRepo.TryAdd(newDir, out errmsg))
            {
                string parentName = parent?.Name ?? "根文件夹";
                _fileDirRepo.SetUpdateTimeAncestrally(parentDir, out errmsg);
                _opRecordRepo.Record(OpRecordOpType.Create, OpRecordTargetType.FileDir,
                    $"在 {parentName} 中新建 {name} ({urlPathName})");
                return true;
            }
            return false;
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
            var wikis = _wikiToDirRepo.GetWikiIdsByDir(dirId).Count;
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
            if(_fileDirRepo.TryRemove(item,out errmsg))
            {
                _opRecordRepo.Record(OpRecordOpType.Remove, OpRecordTargetType.FileDir, $" {item.Name} ");
                return true;
            }
            return false;
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
        public int OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public List<string>? FriendlyPath { get; set; }

        public class FileDirSubDir(int id, string? name, string? urlPathName, DateTime updated)
        {
            public int Id { get; set; } = id;
            public string? Name { get; set; } = name;
            public string? UrlPathName { get; set; } = urlPathName;
            public string? Updated { get; set; } = updated.ToString("yyyy-MM-dd HH:mm");
            public string? OwnerName { get; set; } = "";
            public int ByteCount { get; set; } = 0;
            public int FileNumber { get; set; } = 0;

            public static Dictionary<int, FileDirSubDir> Converter(IQueryable<FileDir> fileDirs)
            {
                var data = fileDirs.Select(x => new FileDirSubDir(x.Id, x.Name, x.UrlPathName, x.Updated)).ToList();
                return data.ToDictionary(x => x.Id, x => x);
            }
        }
        public class FileDirItem(int id, string? name, DateTime updated, int byteCount, string? url)
        {
            public int Id { get; set; } = id;
            public string? Name { get; set; } = name;
            public string? Updated { get; set; } = updated.ToString("yyyy-MM-dd HH:mm");
            public string? OwnerName { get; set; } = "";
            public int ByteCount { get; set; } = byteCount;
            public string? Url { get; set; } = url;

            public static Dictionary<int, FileDirItem> Converter(IQueryable<FileItem> files, Func<string, string> url)
            {
                var data = files.Select(x => new FileDirItem(x.Id, x.DisplayName, x.Updated, x.ByteCount, url(x.StorePathName ?? "??"))).ToList();
                return data.ToDictionary(x => x.Id, x => x);
            }
        }
        public class FileDirWiki(int id, string? name, string? urlPathName, DateTime updated)
        {
            public int Id { get; set; } = id;
            public string? Name { get; set; } = name;
            public string? UrlPathName { get; set; } = urlPathName;
            public string? Updated { get; set; } = updated.ToString("yyyy-MM-dd HH:mm");
            public string? OwnerName { get; set; } = "";
            public static Dictionary<int, FileDirWiki> Converter(IQueryable<WikiItem> wikis)
            {
                var data = wikis.Select(x => new FileDirWiki(x.Id, x.Title, x.UrlPathName, x.Updated)).ToList();
                return data.ToDictionary(x => x.Id, x => x);
            }
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
