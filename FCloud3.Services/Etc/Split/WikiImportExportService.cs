using FCloud3.DbContexts;
using FCloud3.Entities.Files;
using FCloud3.Entities.Table;
using FCloud3.Entities.TextSection;
using FCloud3.Entities.Wiki;
using FCloud3.Repos.Files;
using FCloud3.Repos.Table;
using FCloud3.Repos.TextSec;
using FCloud3.Repos.Wiki;
using FCloud3.Services.Files.Storage.Abstractions;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace FCloud3.Services.Etc.Split
{
    public class WikiImportExportService(
        WikiItemRepo wikiItemRepo,
        WikiParaRepo wikiParaRepo,
        TextSectionRepo textSectionRepo,
        FreeTableRepo freeTableRepo,
        FileItemRepo fileItemRepo,
        WikiRefRepo wikiRefRepo,
        MaterialRepo materialRepo,
        DbTransactionService transaction,
        IStorage storage,
        IFileItemHash fileItemHash)
    {
        /// <summary>
        /// 导出当前用户的所有词条及其中引用的文件
        /// 运行此功能假定当前WikiRefs状态完整
        /// 如果不能保证WikiRefs完整，应先让版主运行InitController中的EnforceParseAll方法
        /// </summary>
        /// <param name="memStream">zip写入的MemoryStream</param>
        /// <param name="uid">用户id，为0表示全部用户</param>
        /// <param name="urlPathNames">指定要导出的词条路径名列表，为空则导出全部</param>
        public void ExportMyWikis(MemoryStream memStream, int uid, List<string>? urlPathNames = null)
        {
            var query = wikiItemRepo.Existing;
            if (uid > 0)
            {
                query = query.Where(x => x.OwnerUserId == uid);
            }
            if (urlPathNames is { Count: > 0 })
            {
                query = query.Where(x => x.UrlPathName != null && urlPathNames.Contains(x.UrlPathName));
            }
            var myWikis = query.ToList();

            if (urlPathNames is { Count: > 0 })
            {
                var foundNames = myWikis
                    .Select(x => x.UrlPathName)
                    .Where(x => x is not null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var notFound = urlPathNames
                    .Where(n => !foundNames.Contains(n))
                    .ToList();
                if (notFound.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"找不到以下词条：{string.Join(", ", notFound)}");
                }
            }
            
            var allParas = wikiParaRepo.Existing
                .Select(x => new { x.WikiItemId, x.NameOverride, x.ObjectId, x.Type, x.Order })
                .ToList();
            var myWikiIds = myWikis.Select(x => x.Id).ToList();
            var myParas = allParas.Where(x => myWikiIds.Contains(x.WikiItemId));
            var myTextIds = myParas
                .Where(x => x.Type == WikiParaType.Text)
                .Select(x => x.ObjectId).ToList();
            var myTableIds = myParas
                .Where(x => x.Type == WikiParaType.Table)
                .Select(x => x.ObjectId).ToList();
            var myFileIds = myParas
                .Where(x => x.Type == WikiParaType.File)
                .Select(x => x.ObjectId).ToList();
            var myTexts = textSectionRepo.Existing
                .Where(x => myTextIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Title, x.Content }).ToList();
            var myTables = freeTableRepo.Existing
                .Where(x => myTableIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.Data }).ToList();
            var myFiles = fileItemRepo.Existing
                .Where(x => myFileIds.Contains(x.Id))
                .Select(x => new { x.Id, x.DisplayName, x.StorePathName }).ToList();

            var exportedWikis = new List<ExportedWiki>();
            var attachments = new AttachmentsSummary(storage.GetUrlBase());
            foreach (var w in myWikis)
            {
                var exw = new ExportedWiki(w);
                var itsParas = allParas
                    .Where(x => x.WikiItemId == w.Id)
                    .OrderBy(x => x.Order);
                foreach (var p in itsParas)
                {
                    ExportedWiki.ExportedWikiPara? para = null;
                    if (p.Type == WikiParaType.Text)
                    {
                        var text = myTexts.Find(x => x.Id == p.ObjectId);
                        if (text is { })
                            para = new(text.Title, p.NameOverride, p.Type, text.Content);
                    }
                    else if (p.Type == WikiParaType.Table)
                    {
                        var table = myTables.Find(x => x.Id == p.ObjectId);
                        if (table is { })
                            para = new(table.Name, p.NameOverride, p.Type, table.Data);
                    }
                    else if (p.Type == WikiParaType.File)
                    {
                        var file = myFiles.Find(x => x.Id == p.ObjectId);
                        if (file is { })
                        {
                            para = new(file.DisplayName, p.NameOverride, p.Type, file.StorePathName);
                            if (file.StorePathName is { })
                                attachments.FileItems.Add(file.StorePathName);
                        }
                    }
                    if (para is { })
                        exw.Paras.Add(para);
                }
                var refTexts = wikiRefRepo.WikiRefs
                    .Where(x => x.WikiId == w.Id)
                    .Select(x => x.Str).ToList();
                var refedMats = materialRepo
                    .CachedItemsByPred(x => refTexts.Contains(x.Name))
                    .Select(x => x.PathName);
                foreach (var mat in refedMats)
                {
                    if (mat is { })
                        attachments.Materials.Add(mat);
                }
                exportedWikis.Add(exw);
            }

            using var archive = new ZipArchive(memStream, ZipArchiveMode.Create, true);
            var jsonSerializer = JsonSerializer.CreateDefault();
            jsonSerializer.Formatting = Formatting.Indented;
            foreach (var w in exportedWikis)
            {
                var fileName = GetZipEntryNameForWiki(w.Info.Title);
                var wikiEntry = archive.CreateEntry(fileName);
                using var stream = wikiEntry.Open();
                using var writer = new StreamWriter(stream);
                jsonSerializer.Serialize(writer, w);
                writer.Flush();
            }
            string attachmentsEntryName = "词条附件.json";
            var attachmentsEntry = archive.CreateEntry(attachmentsEntryName);
            using (var attachmentsStream = attachmentsEntry.Open())
            {
                using var attachmentsWriter = new StreamWriter(attachmentsStream);
                jsonSerializer.Serialize(attachmentsWriter, attachments);
                attachmentsWriter.Flush();
            }

            var readmeEntry = archive.CreateEntry("说明.txt");
            using (var readmeStream = readmeEntry.Open())
            {
                using var readmeWriter = new StreamWriter(readmeStream);
                var readmeContent = $"""
                你好：
                本压缩包内是你的所有词条，以及它们引用的文件链接。
                如需将内容导入另一个FCloud3网站，把本压缩包交给该网站管理员。
                请注意，本压缩包仅备份了文本内容，词条中引用的文件依然在原有服务器上。
                如果需要下载到本地，见"{attachmentsEntryName}"文件。
                在另一网站导入时，新网站会自动获取和存储这些引用文件，无需任何操作。

                除非你知道你在做什么，否则不要随意修改文件中的任何内容，以免造成格式损坏。
                """;
                readmeWriter.Write(readmeContent);
                readmeWriter.Flush();
            }

            archive.Dispose();
        }

        /// <summary>
        /// 导入词条压缩包
        /// </summary>
        /// <param name="stream">zip文件流</param>
        /// <param name="uid">导入到的用户id</param>
        /// <param name="errmsg">错误信息</param>
        /// <returns>成功导入的词条数量</returns>
        public int ImportWikis(Stream stream, int uid, out string? errmsg, int? targetUserId = null)
        {
            errmsg = null;
            List<ExportedWiki> importedWikis = [];
            AttachmentsSummary? attachments = null;

            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var jsonSerializer = JsonSerializer.CreateDefault();

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(wikiDirNameInZip + "/") && entry.FullName.EndsWith(".f3w.json"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var wiki = jsonSerializer.Deserialize<ExportedWiki>(new JsonTextReader(reader));
                        if (wiki is not null)
                            importedWikis.Add(wiki);
                    }
                    else if (entry.FullName == "词条附件.json")
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        attachments = jsonSerializer.Deserialize<AttachmentsSummary>(new JsonTextReader(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                errmsg = $"解析压缩包失败：{ex.Message}";
                return 0;
            }

            if (importedWikis.Count == 0)
            {
                errmsg = "压缩包中未找到词条文件";
                return 0;
            }

            int successCount = 0;
            var urlBase = attachments?.UrlBase ?? storage.GetUrlBase();
            var urlBaseSlashEnded = string.IsNullOrEmpty(urlBase) || urlBase.EndsWith('/') ? urlBase : urlBase + "/";

            foreach (var wiki in importedWikis)
            {
                // 每个词条独立处理，清理变更追踪器，避免上一个词条残留的已追踪实体
                // 与当前词条的新实体发生主键冲突
                wikiItemRepo.ChangeTracker.Clear();

                string? title = wiki.Info.Title;
                string? urlPathName = wiki.Info.UrlPathName;
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(urlPathName))
                    continue;

                // 如果路径名已存在，尝试添加后缀
                string originalUrlPathName = urlPathName;
                int suffix = 1;
                while (wikiItemRepo.GetByUrlPathNameFromCache(urlPathName) is not null)
                {
                    urlPathName = $"{originalUrlPathName}_{suffix}";
                    suffix++;
                    if (urlPathName.Length > WikiItem.urlPathNameMaxLength)
                    {
                        urlPathName = urlPathName[..WikiItem.urlPathNameMaxLength];
                        break;
                    }
                }

                var newWiki = new WikiItem
                {
                    Title = title,
                    UrlPathName = urlPathName,
                    Description = wiki.Info.Description,
                    LastActive = wiki.Info.LastActive
                };

                string? paraErrmsg = null;
                bool paraSuccess = transaction.DoTransaction(() =>
                {
                    if (!wikiItemRepo.StageAdd(newWiki, out paraErrmsg, targetUserId))
                        return false;

                    var textSections = new List<TextSection>();
                    var freeTables = new List<FreeTable>();
                    var fileItems = new List<FileItem>();
                    var paraInfos = new List<(ExportedWiki.ExportedWikiPara Source, TextSection? Text, FreeTable? Table, FileItem? File, int ExistingFileId)>();

                    foreach (var para in wiki.Paras)
                    {
                        if (para.ParaType == WikiParaType.Text)
                        {
                            var content = para.Data ?? "";
                            var brief = content.Length >= 30
                                ? string.Concat(content.AsSpan(0, 27), "...")
                                : content;
                            var textSection = new TextSection
                            {
                                Title = para.ObjName ?? "",
                                Content = content,
                                ContentBrief = brief
                            };
                            textSections.Add(textSection);
                            paraInfos.Add((para, textSection, null, null, 0));
                        }
                        else if (para.ParaType == WikiParaType.Table)
                        {
                            var tableData = FreeTableDataConvert.Deserialize(para.Data);
                            if (!freeTableRepo.StageCreateWithContent(tableData, para.ObjName ?? "", out var table, out var tableErrmsg))
                            {
                                paraErrmsg = tableErrmsg ?? "创建表格失败";
                                return false;
                            }
                            freeTables.Add(table!);
                            paraInfos.Add((para, null, table, null, 0));
                        }
                        else if (para.ParaType == WikiParaType.File)
                        {
                            // 尝试从远程获取文件并保存
                            var filePathName = para.Data;
                            if (!string.IsNullOrWhiteSpace(filePathName))
                            {
                                string fileUrl;
                                if (filePathName.StartsWith("http://") || filePathName.StartsWith("https://"))
                                    fileUrl = filePathName;
                                else if (!string.IsNullOrEmpty(urlBaseSlashEnded))
                                    fileUrl = urlBaseSlashEnded + filePathName;
                                else
                                    fileUrl = filePathName;

                                try
                                {
                                    byte[] fileBytes;
                                    if (fileUrl.StartsWith("http://") || fileUrl.StartsWith("https://"))
                                    {
                                        using var httpClient = new System.Net.Http.HttpClient();
                                        fileBytes = httpClient.GetByteArrayAsync(fileUrl).GetAwaiter().GetResult();
                                    }
                                    else
                                    {
                                        var localStream = storage.Read(filePathName);
                                        if (localStream is null)
                                            throw new Exception("本地存储中找不到文件");
                                        using var ms = new MemoryStream();
                                        localStream.CopyTo(ms);
                                        fileBytes = ms.ToArray();
                                    }
                                    using var fileStream = new MemoryStream(fileBytes);
                                    var displayName = para.ObjName ?? Path.GetFileName(filePathName) ?? "imported_file";
                                    var storePath = "wikiFile";
                                    var hash = fileItemHash.Hash(fileBytes);

                                    var existingFile = fileItemRepo.Existing
                                        .Where(x => x.Hash == hash)
                                        .FirstOrDefault();
                                    if (existingFile is not null)
                                    {
                                        paraInfos.Add((para, null, null, null, existingFile.Id));
                                    }
                                    else
                                    {
                                        var newFile = new FileItem
                                        {
                                            DisplayName = displayName,
                                            StorePathName = $"{storePath}/{Guid.NewGuid():N}{Path.GetExtension(displayName)}",
                                            ByteCount = fileBytes.Length,
                                            Hash = hash
                                        };
                                        if (storage.Save(fileStream, newFile.StorePathName, out var storageErrmsg))
                                        {
                                            fileItems.Add(newFile);
                                            paraInfos.Add((para, null, null, newFile, 0));
                                        }
                                        else
                                        {
                                            paraErrmsg = storageErrmsg ?? "保存文件失败";
                                            return false;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    paraErrmsg = $"获取文件失败：{ex.Message}";
                                    return false;
                                }
                            }
                        }
                    }

                    textSectionRepo.BatchPrepare(textSections);
                    freeTableRepo.BatchPrepare(freeTables);
                    fileItemRepo.BatchPrepare(fileItems);

                    // 第一次保存：让 WikiItem、TextSection、FreeTable、FileItem 获得真实 Id
                    wikiItemRepo.SaveChanges();

                    // 第二次保存：用真实 Id 创建 WikiPara
                    int order = 0;
                    var wikiParasToAdd = paraInfos.Select(info => new WikiPara
                    {
                        WikiItemId = newWiki.Id,
                        ObjectId = info.Text?.Id ?? info.Table?.Id ?? info.File?.Id ?? info.ExistingFileId,
                        Type = info.Source.ParaType,
                        Order = order++,
                        NameOverride = info.Source.ParaName
                    }).ToList();
                    wikiParaRepo.BatchPrepare(wikiParasToAdd);
                    wikiItemRepo.SaveChanges();

                    return true;
                });
                if (paraErrmsg is not null)
                    errmsg = paraErrmsg;

                if (paraSuccess)
                {
                    wikiItemRepo.NotifyRefUpdated();
                    successCount++;
                }
            }

            if (successCount == 0 && errmsg is null)
                errmsg = "没有词条被成功导入";

            return successCount;
        }

        /// <summary>
        /// 预览导入内容，不实际创建任何数据
        /// </summary>
        public ImportPreview? PreviewImport(Stream stream, out string? errmsg)
        {
            errmsg = null;
            List<ExportedWiki> importedWikis = [];
            AttachmentsSummary? attachments = null;

            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var jsonSerializer = JsonSerializer.CreateDefault();

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(wikiDirNameInZip + "/") && entry.FullName.EndsWith(".f3w.json"))
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        var wiki = jsonSerializer.Deserialize<ExportedWiki>(new JsonTextReader(reader));
                        if (wiki is not null)
                            importedWikis.Add(wiki);
                    }
                    else if (entry.FullName == "词条附件.json")
                    {
                        using var entryStream = entry.Open();
                        using var reader = new StreamReader(entryStream);
                        attachments = jsonSerializer.Deserialize<AttachmentsSummary>(new JsonTextReader(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                errmsg = $"解析压缩包失败：{ex.Message}";
                return null;
            }

            if (importedWikis.Count == 0)
            {
                errmsg = "压缩包中未找到词条文件";
                return null;
            }

            var urlBase = attachments?.UrlBase ?? storage.GetUrlBase();
            var urlBaseSlashEnded = string.IsNullOrEmpty(urlBase) || urlBase.EndsWith('/') ? urlBase : urlBase + "/";
            var preview = new ImportPreview
            {
                UrlBase = urlBase
            };

            foreach (var wiki in importedWikis)
            {
                string? title = wiki.Info.Title;
                string? urlPathName = wiki.Info.UrlPathName;
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(urlPathName))
                    continue;

                string originalUrlPathName = urlPathName;
                string resolvedUrlPathName = urlPathName;
                bool hasConflict = wikiItemRepo.GetByUrlPathNameFromCache(urlPathName) is not null;

                if (hasConflict)
                {
                    int suffix = 1;
                    while (wikiItemRepo.GetByUrlPathNameFromCache(resolvedUrlPathName) is not null)
                    {
                        resolvedUrlPathName = $"{originalUrlPathName}_{suffix}";
                        suffix++;
                        if (resolvedUrlPathName.Length > WikiItem.urlPathNameMaxLength)
                        {
                            resolvedUrlPathName = resolvedUrlPathName[..WikiItem.urlPathNameMaxLength];
                            break;
                        }
                    }
                }

                var wikiPreview = new WikiPreviewItem
                {
                    Title = title,
                    OriginalUrlPathName = originalUrlPathName,
                    ResolvedUrlPathName = resolvedUrlPathName,
                    HasConflict = hasConflict
                };

                foreach (var para in wiki.Paras)
                {
                    wikiPreview.ParaTypes.Add((byte)para.ParaType);
                    if (para.ParaType == WikiParaType.File && !string.IsNullOrWhiteSpace(para.Data))
                    {
                        string fileUrl;
                        if (para.Data.StartsWith("http://") || para.Data.StartsWith("https://"))
                        {
                            fileUrl = para.Data;
                        }
                        else if (!string.IsNullOrEmpty(urlBaseSlashEnded))
                        {
                            fileUrl = urlBaseSlashEnded + para.Data;
                        }
                        else
                        {
                            fileUrl = para.Data;
                        }
                        preview.Files.Add(new FilePreviewItem
                        {
                            DisplayName = para.ObjName ?? Path.GetFileName(para.Data) ?? "unknown",
                            StorePathName = para.Data,
                            FullUrl = fileUrl
                        });
                    }
                }

                preview.Wikis.Add(wikiPreview);
            }

            return preview;
        }

        private readonly static string fileNameInvalidChars
            = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        private readonly static string fileNameRegexInvalidPattern
            = $"[{fileNameInvalidChars}]";
        private const string wikiDirNameInZip = "词条";
        private static string GetZipEntryNameForWiki(string? wTitle)
        {
            var fileExt = Path.GetRandomFileName();
            fileExt = Path.ChangeExtension(fileExt, ".f3w.json");
            wTitle ??= "";
            if(wTitle.Length > 32)
                wTitle = wTitle[..32];
            var fileName = Path.ChangeExtension(wTitle, fileExt);
            
            string validFileName = Regex.Replace(fileName, fileNameRegexInvalidPattern, "_");
            return $"{wikiDirNameInZip}/{validFileName}";
        }

        public class ExportedWiki(WikiItem info)
        {
            public ExportedWikiInfo Info { get; } = new(info);
            public List<ExportedWikiPara> Paras { get; } = [];

            public class ExportedWikiInfo(WikiItem infoSource)
            {
                public string? Title { get; } = infoSource.Title;
                public string? UrlPathName { get; } = infoSource.UrlPathName;
                public string? Description { get; } = infoSource.Description;
                public DateTime LastActive { get; } = infoSource.LastActive;
            }
            public class ExportedWikiPara(
                string? objName, string? paraName, WikiParaType paraType, string? data)
            {
                public string? ObjName { get; } = objName;
                public string? ParaName { get; } = paraName;
                public WikiParaType ParaType { get; } = paraType;
                public string? Data { get; } = data;
            }
        }
        public class AttachmentsSummary(string urlBase)
        {
            public string UrlBase { get; } = urlBase;
            public HashSet<string> Materials { get; } = [];
            public HashSet<string> FileItems { get; } = [];
        }

        public class ImportPreview
        {
            public List<WikiPreviewItem> Wikis { get; set; } = [];
            public List<FilePreviewItem> Files { get; set; } = [];
            public string? UrlBase { get; set; }
        }

        public class WikiPreviewItem
        {
            public string? Title { get; set; }
            public string? OriginalUrlPathName { get; set; }
            public string? ResolvedUrlPathName { get; set; }
            public bool HasConflict { get; set; }
            public List<byte> ParaTypes { get; set; } = [];
        }

        public class FilePreviewItem
        {
            public string? DisplayName { get; set; }
            public string? StorePathName { get; set; }
            public string? FullUrl { get; set; }
        }

        public List<FileStatusResult> CheckFileStatus(List<string> urls)
        {
            var results = new List<FileStatusResult>();
            using var httpClient = new System.Net.Http.HttpClient();
            foreach (var url in urls)
            {
                try
                {
                    var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);
                    var resp = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    results.Add(new FileStatusResult
                    {
                        Url = url,
                        Accessible = resp.IsSuccessStatusCode,
                        Size = resp.Content.Headers.ContentLength
                    });
                }
                catch
                {
                    results.Add(new FileStatusResult { Url = url, Accessible = false });
                }
            }
            return results;
        }

        public class FileStatusResult
        {
            public string? Url { get; set; }
            public bool Accessible { get; set; }
            public long? Size { get; set; }
        }
    }
}
