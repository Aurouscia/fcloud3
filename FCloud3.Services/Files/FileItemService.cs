﻿using FCloud3.Repos.Files;
using FCloud3.Entities.Files;
using System.Text.RegularExpressions;
using FCloud3.Services.Files.Storage.Abstractions;

namespace FCloud3.Services.Files
{
    public class FileItemService
    {
        private readonly FileItemRepo _fileItemRepo;
        private readonly FileDirRepo _fileDirRepo;
        private readonly IStorage _storage;
        private readonly IFileStreamHasher _fileStreamHasher;

        public FileItemService(FileItemRepo fileItemRepo, FileDirRepo fileDirRepo, IStorage storage, IFileStreamHasher fileStreamHasher)
        {
            _fileItemRepo = fileItemRepo;
            _fileDirRepo = fileDirRepo;
            _storage = storage;
            _fileStreamHasher = fileStreamHasher;
        }
        
        public FileItemDetail? GetDetail(int id, out string? errmsg)
        {
            var item = _fileItemRepo.GetById(id);
            if (item is null)
            {
                errmsg = "找不到指定id的文件";
                return null;
            }
            var path = _fileDirRepo.GetPathById(item.InDir);
            if(path is null)
            {
                errmsg = "寻找指定文件的文件夹路径时出错";
                return null;
            }
            FileItemDetail d = new()
            {
                ItemInfo = item,
                DirPath = path,
            };
            errmsg = null;
            return d;
        }
        public int Save(Stream stream,int byteCount, string displayName, string storePath, string? storeName, out string? errmsg)
        {
            if(storeName is null)
            {
                string ext = Path.GetExtension(displayName);
                string randName = Path.GetRandomFileName();
                storeName = Path.ChangeExtension(randName, ext);
            }
            var storePathName = StorePathName(storePath, storeName,out errmsg);
            if(storePathName is null) { return 0; }
            string hash = _fileStreamHasher.Hash(stream, out stream);
            FileItem f = new()
            {
                DisplayName = displayName,
                StorePathName = storePathName,
                ByteCount = byteCount,
                Hash = hash
            };
            if (!_fileItemRepo.TryAddCheck(f, out errmsg)) { return 0; }

            if (!_storage.Save(stream, storePathName, out errmsg))
                return 0;
            stream.Close();
            return _fileItemRepo.TryAddAndGetId(f, out errmsg);
        }

        private static string[] invalidChars = new[] { "/", "..", "\\" };
        public string? StorePathName(string storePath, string storeName, out string? errmsg)
        {
            string pathName = storePath + "/" + storeName;
            if (pathName.Length > FileItem.storePathNameMaxLength)
            {
                errmsg = "存储名过长，请缩短";
                return null;
            }
            if (invalidChars.Any(storeName.Contains))
            {
                errmsg = $"存储名不能含有{string.Join('或', invalidChars)}";
                return null;
            }
            errmsg = null;
            return pathName;
        }

        public string? Url(int id)
        {
            string? storePathName = _fileItemRepo.GetStorePathName(id);
            if (storePathName is null)
                return null;
            return _storage.FullUrl(storePathName);
        }

        public class FileItemDetail
        {
            public FileItem? ItemInfo { get; set; }
            public string[]? DirPath { get; set; }
        }
    }
}
