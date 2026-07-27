using FCloud3.DbContexts;
using FCloud3.Entities.Wiki;
using FCloud3.Repos.Etc;
using System.Text.RegularExpressions;
using FCloud3.Repos.Sys;
using FCloud3.Entities.Sys;
using Microsoft.EntityFrameworkCore;

namespace FCloud3.Repos.Wiki
{
    public class WikiItemRepo(
        FCloudContext context,
        ICommitingUserIdProvider userIdProvider) 
        : RepoBaseCache<WikiItem, WikiItemCacheModel>(context, userIdProvider)
    {
        private const string validUrlPathNamePattern = @"^[A-Za-z0-9\-_]{1,}$";

        public IQueryable<WikiItem> ExistingAndNotSealed => Existing.Where(x => !x.Sealed);
        public IQueryable<WikiItem> ExistingAndNotSealedAndEdited
            => ExistingAndNotSealed.Where(x => x.Created < x.Updated);
        public IQueryable<WikiItem> QuickSearch(string str, bool includeSealed = false, int includeOwnedBy = 0)
        {
            IQueryable<WikiItem> q;
            if (includeSealed)
                q = Existing;
            else
                q = Existing.Where(x => !x.Sealed || x.OwnerUserId == includeOwnedBy);
            return q
                .Where(x => 
                    (x.Title != null && x.Title.Contains(str) 
                     || (x.UrlPathName != null && x.UrlPathName.Contains(str))))
                .OrderBy(x => x.Title!.Length)
                .ThenByDescending(x => x.Updated);
        }
        public IQueryable<WikiItem> GetByUrlPathName(string urlPathName)
        {
            return Existing.Where(x => x.UrlPathName == urlPathName);
        }
        public WikiItemCacheModel? GetByUrlPathNameFromCache(string urlPathName)
        {
            return CachedItemByPred(x => x.UrlPathName == urlPathName);
        }

        public override int GetOwnerIdById(int id)
        {
            return Existing.Where(x => x.Id == id).Select(x => x.OwnerUserId).FirstOrDefault();
        }

        public int TryAddAndGetId(WikiItem item, out string? errmsg, int? overrideOwnerUserId = null)
        {
            if (!InfoCheck(item, false, out errmsg))
                return 0;
            item.OwnerUserId = overrideOwnerUserId ?? _userIdProvider.Get();
            var now = DateTime.Now;
            item.LastActive = now;
            base.Add(item, now);
            UpdateWikiItemRefLu(now);
            return item.Id;
        }
        public bool StageAdd(WikiItem item, out string? errmsg, int? overrideOwnerUserId = null)
        {
            if (!InfoCheck(item, false, out errmsg))
                return false;
            item.OwnerUserId = overrideOwnerUserId ?? _userIdProvider.Get();
            var now = DateTime.Now;
            item.LastActive = now;
            base.BatchPrepare([item], now);
            return true;
        }
        public void NotifyRefUpdated()
        {
            UpdateWikiItemRefLu(DateTime.Now);
        }

        public bool TryUpdate(WikiItem item, out string? errmsg)
        {
            if (!InfoCheck(item, true, out errmsg))
                return false;
            var now = DateTime.Now;
            base.Update(item, now);
            UpdateWikiItemRefLu(now);
            return true;
        }
        public bool TryRemove(WikiItem item, out string? errmsg)
        {
            var now = DateTime.Now;
            base.Remove(item, now);
            UpdateWikiItemRefLu(now);
            errmsg = null;
            return true;
        }
        public bool InfoCheck(WikiItem item, bool existing, out string? errmsg)
        {
            errmsg = null;
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                errmsg = "词条标题不能为空";
                return false;
            }
            if (string.IsNullOrWhiteSpace(item.UrlPathName))
            {
                errmsg = "词条路径名不能为空";
                return false;
            }
            if (item.Title.Length > WikiItem.titleMaxLength)
            {
                errmsg = $"词条标题不能超过{WikiItem.titleMaxLength}个字符";
                return false;
            }
            if (item.UrlPathName.Length > WikiItem.urlPathNameMaxLength)
            {
                errmsg = $"词条路径名不能超过{WikiItem.urlPathNameMaxLength}个字符";
                return false;
            }
            if (!Regex.IsMatch(item.UrlPathName, validUrlPathNamePattern))
            {
                errmsg = "路径名只能有英文字母，数字、下划线和\"-\"";
                return false;
            }
            int descMaxLength = 128;
            if (item.Description?.Length > descMaxLength)
            {
                errmsg = $"词条简介不能超过{descMaxLength}字符";
                return false;
            }
            string? conflict = null;
            if (existing)
            {
                conflict = Existing.Where(x => x.UrlPathName == item.UrlPathName)
                    .Where(x=>x.Id!=item.Id)
                    .Select(x => x.Title).FirstOrDefault();
            }
            else
            {
                conflict = Existing.Where(x => x.UrlPathName == item.UrlPathName)
                    .Select(x => x.Title).FirstOrDefault();
            }
            if (conflict is not null)
            {
                errmsg = $"冲突：已有同样路径名的其他词条【{conflict}】";
                return false;
            }
            return true;
        }

        public int UpdateTimeAndLuAndWikiActive(IQueryable<int> wikiIds, bool updateWikiActive)
        {
            var now = DateTime.Now;
            int affectedCount = base.UpdateTime(wikiIds, now);
            if (updateWikiActive)
            {
                Existing.Where(x => wikiIds.Contains(x.Id))
                    .ExecuteUpdate(spc => spc.SetProperty(
                        x => x.LastActive, now));
            }
            return affectedCount;
        }
        public int UpdateTimeAndLuAndWikiActive(int wikiId, bool updateWikiActive)
        {
            var ids = new int[] { wikiId };
            return UpdateTimeAndLuAndWikiActive(ids.AsQueryable(), updateWikiActive);
        }
        public int UpdateTimeAndLuAndWikiActive(List<int> wikiIds, bool updateWikiActive)
        {
            return UpdateTimeAndLuAndWikiActive(wikiIds.AsQueryable(), updateWikiActive);
        }
        private void UpdateWikiItemRefLu(DateTime time)
        {
            LastUpdateDbUtil.SetLastUpdateFor(_ctx, LastUpdateType.WikiItemRefedProps, time);
        }

        protected override LastUpdateType GetLastUpdateType()
            => LastUpdateType.WikiItem;
        public override IQueryable<WikiItem> OwnedByUser(int uid = -1)
        {
            if (uid == -1)
                return Existing;
            return Existing.Where(x => x.OwnerUserId == uid);
        }

        protected override IQueryable<WikiItemCacheModel> ConvertToCacheModel(IQueryable<WikiItem> q)
        {
            return q.Select(x => new WikiItemCacheModel(
                x.Id, x.Updated, x.Sealed, x.OwnerUserId, x.Title, x.UrlPathName));
        }
        public IEnumerable<WikiItemCacheModel> CachedNotSealed()
            => CachedItemsByPred(x => !x.Sealed);
    }

    public class WikiItemCacheModel(
        int id, DateTime updated,
        bool @sealed, int ownerId, string? title, string? urlPathName)
        : CacheModelBase<WikiItem>(id, updated)
    {
        public int OwnerId { get; } = ownerId;
        public bool Sealed { get; } = @sealed;
        public string? Title { get; } = title;
        public string? UrlPathName { get; } = urlPathName;
    }
}
