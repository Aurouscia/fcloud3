using FCloud3.DbContexts;
using FCloud3.Entities;
using FCloud3.Repos.Etc;
using FCloud3.Repos.Etc.Index;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FCloud3.Repos
{
    public abstract class RepoBase<T> where T : class,IDbModel
    {
        private readonly FCloudContext _context;
        protected readonly ICommitingUserIdProvider _userIdProvider;

        public RepoBase(FCloudContext context, ICommitingUserIdProvider userIdProvider)
        {
            _context = context;
            _userIdProvider = userIdProvider;
        }

        public IQueryable<T> All => _context.Set<T>();
        public IQueryable<T> Existing => _context.Set<T>().Where(x => x.Deleted == false);
        public IQueryable<T> Deleted => _context.Set<T>().Where(x => x.Deleted);
        public IQueryable<T> ExistingExceptId(int id) => Existing.Where(x => x.Id != id);
        public int ExistingCount => Existing.Count();
        public ChangeTracker ChangeTracker => _context.ChangeTracker;
        public void SaveChanges() => _context.SaveChanges();
        public virtual IQueryable<T> OwnedByUser(int uid = -1)
        {
            if (uid == -1)
                return Existing;
            return Existing.Where(x => x.CreatorUserId == uid);
        }

        public IQueryable<T> IndexFilterOrder(IndexQuery query)
        {
            return IndexFilterOrder(Existing, query);
        }
        public IQueryable<T> IndexFilterOrder(IndexQuery query, Func<string, string> keyReplace)
        {
            return IndexFilterOrder(Existing, query, keyReplace);
        }
        public IQueryable<T> IndexFilterOrder(IQueryable<T> from, IndexQuery query, Func<string,string>? keyReplace = null)
        {
            var q = from;
            var parsedSearch = query.ParsedSearch();
            var orderBy = query.OrderBy;
            if (keyReplace is not null)
            {
                parsedSearch.ForEach(s => { s.Key = keyReplace(s.Key); });
                if(orderBy is not null)
                    orderBy = keyReplace(orderBy);
            }
            q = Filter(q, parsedSearch);
            if (orderBy != null)
                q = Order(q, orderBy, query.OrderRev);
            else
                q = q.OrderByDescending(x => x.Updated);
            return q;
        }
        public IndexResult<T> Index(IndexQuery query)
        {
            return  IndexFilterOrder(query).TakePageAndConvertOneByOne(query,x=>x);
        }
        public IQueryable<T> Filter(IQueryable<T> q, SearchDict dict)
        {
            if (dict.Count==0)
                return q;
            Type t = typeof(T);
            var props = t.GetProperties();
            Lazy<MethodInfo> stringContain = new(() =>
            {
                var contains = typeof(string).GetMethods();
                return contains.First(x =>
                {
                    if (x.Name != nameof(string.Contains) || x.ContainsGenericParameters)
                        return false;
                    var ps = x.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(string))
                        return false;
                    return true;
                });
            });
            foreach (var search in dict)
            {
                var p = props.FirstOrDefault(x => x.Name == search.Key);
                if (p is null) continue;

                LambdaExpression lambda;
                ParameterExpression param = Expression.Parameter(t, "x");
                MemberExpression member = Expression.Property(param, p);
                if (p.PropertyType == typeof(string))
                {
                    ConstantExpression stringValue = Expression.Constant(search.Value);

                    var contain = Expression.Call(member, stringContain.Value ,stringValue);
                    lambda = Expression.Lambda(contain, param);
                }
                else if (p.PropertyType == typeof(int))
                {
                    if (int.TryParse(search.Value, out var value))
                    {
                        ConstantExpression intValue = Expression.Constant(value);
                        lambda = Expression.Lambda(Expression.Equal(member, intValue), param);
                    }
                    else
                        continue;
                }
                else
                    continue;
                var selector = lambda as Expression<Func<T, bool>>;
                if (selector is not null)
                {
                    q = q.Where(selector);
                }
            }
            return q;
        }
        public IQueryable<T> Order(IQueryable<T> q, string? orderBy, bool rev)
        {
            if (string.IsNullOrEmpty(orderBy))
                return q;
            Type t = typeof(T);
            var props = t.GetProperties();

            static IQueryable<T> sort<TBy>(IQueryable<T> q ,LambdaExpression lambda, bool rev)
            {
                var selector = lambda as Expression<Func<T, TBy>>;
                if (selector is not null)
                    q = rev ? q.OrderByDescending(selector) : q.OrderBy(selector);
                return q;
            }

            foreach(var prop in props)
            {
                Type propType = prop.PropertyType;
                if (prop.Name == orderBy)
                {
                    ParameterExpression param = Expression.Parameter(t, "x");
                    MemberExpression member = Expression.Property(param, prop);
                    LambdaExpression lambda = Expression.Lambda(member, param);
                    if (propType == typeof(string))
                        q = sort<string>(q, lambda, rev);
                    else if (propType == typeof(int))
                        q = sort<int>(q, lambda, rev);
                    else if (propType == typeof(DateTime))
                        q = sort<DateTime>(q, lambda, rev);
                }
            }
            return q;
        }

        public T? GetById(int id)
        {
            return Existing.Where(x => x.Id == id).FirstOrDefault();
        }
        public T GetByIdEnsure(int id)
        {
            return Existing.Where(x => x.Id == id).FirstOrDefault() ?? throw new Exception("找不到目标");
        }
        public IQueryable<T> GetqById(int id)
        {
            return Existing.Where(x => x.Id == id);
        }
        public IQueryable<T> GetRangeByIds(List<int> ids)
        {
            if (ids.Count == 0) 
                return new List<T>().AsQueryable();
            return Existing.Where(x => ids.Contains(x.Id));
        }
        public IQueryable<T> GetRangeByIds(IQueryable<int> ids)
        {
            return Existing.Where(x => ids.Contains(x.Id));
        }
        public List<T2> GetRangeByIdsOrdered<T2>(List<int> ids, Func<IQueryable<T>, Dictionary<int, T2>> converter)
        {
            var vals = converter(GetRangeByIds(ids));
            if(vals.Count == 0) 
                return [];
            var res = new List<T2>();
            foreach(var id in ids)
            {
                vals.TryGetValue(id, out T2? val);
                if(val is not null)
                {
                    res.Add(val);
                }
            }
            return res;
        }
        public virtual int GetOwnerIdById(int id)
        {
            return Existing.Where(x => x.Id == id).Select(x => x.CreatorUserId).FirstOrDefault();
        }

        protected virtual void Add(T item, DateTime? time = null)
        {
            item.CreatorUserId = _userIdProvider.Get();
            //仅获取一次当前时间才能确保完全一致，
            //可通过判断创建时间==更新时间来判断该对象是否新建
            DateTime now = time ?? DateTime.Now;
            BeforeDataChange(now);
            item.Created = now;
            item.Updated = now;
            _context.Add(item);
            _context.SaveChanges();
            AfterDataChange();
        }
        protected virtual void AddRange(List<T> items, DateTime? time = null)
        {
            BatchPrepare(items, time);
            _context.SaveChanges();
            AfterDataChange();
        }

        /// <summary>
        /// 将实体标记为 Added 并填充 Created/Updated/CreatorUserId，但不调用 SaveChanges。
        /// 适用于外部需要批量控制保存时机的场景。
        /// </summary>
        public virtual void BatchPrepare(List<T> items, DateTime? time = null)
        {
            int uid = _userIdProvider.Get();
            //仅获取一次当前时间才能确保完全一致，
            //可通过判断创建时间==更新时间来判断该对象是否新建
            DateTime now = time ?? DateTime.Now;
            BeforeDataChange(now);
            foreach (var item in items)
            {
                item.Created = now;
                item.Updated = now;
                item.CreatorUserId = uid;
                _context.Add(item);
            }
        }
        protected int AddAndGetId(T item)
        {
            Add(item);
            return item.Id;
        }
        public int AddDefaultAndGetId()
        {
            var item = NewDefaultObject() ?? throw new NotImplementedException();
            return AddAndGetId(item);
        }
        public virtual T? NewDefaultObject() 
        { 
            return null; 
        }

        protected virtual void Update(T item, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            item.Updated = t;
            _context.Update(item);
            _context.SaveChanges();
            AfterDataChange();
        }
        protected virtual void UpdateRange(List<T> items, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            foreach (var item in items)
            {
                item.Updated = t;
                _context.Update(item);
            }
            _context.SaveChanges();
            AfterDataChange();
        }
        protected virtual void UpdateRangeByDelegateLocally(
            Func<T, bool> selector, Action<T> operations, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            var items = Existing.Where(selector).ToList();
            items.ForEach(item =>
            {
                operations(item);
                item.Updated = t;
            });
            _context.SaveChanges();
            AfterDataChange();
        }
        
        public void UpdateTime(int id, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            Existing.Where(x => x.Id == id)
                .ExecuteUpdate(x => x.SetProperty(m => m.Updated, t));
            AfterDataChange();
        }
        public virtual void UpdateTime(List<int> ids, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            Existing.Where(x => ids.Contains(x.Id))
                .ExecuteUpdate(x => x.SetProperty(m => m.Updated, t));
            AfterDataChange();
        }
        public virtual int UpdateTime(IQueryable<int> ids, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            var changedCount = Existing.Where(x => ids.Contains(x.Id))
                .ExecuteUpdate(x => x.SetProperty(m => m.Updated, t));
            AfterDataChange();
            return changedCount;
        }

        protected virtual void Remove(T item, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            item.Deleted = true;
            item.Updated = t;
            _context.Update(item);
            _context.SaveChanges();
            AfterDataChange();
        }
        protected virtual void RemoveRange(List<T> items, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            items.ForEach(item =>
            {
                item.Deleted = true;
                item.Updated = t;
                _context.Update(item);
            });
            _context.SaveChanges();
            AfterDataChange();
        }
        protected virtual void Remove(int id, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            Existing.Where(x => x.Id == id)
                .ExecuteUpdate(x => x
                    .SetProperty(m => m.Deleted, true)
                    .SetProperty(m => m.Updated, t));
            AfterDataChange();
        }
        protected virtual void RemoveRange(List<int> ids, DateTime? time = null)
        {
            var t = time ?? DateTime.Now;
            BeforeDataChange(t);
            Existing.Where(x => ids.Contains(x.Id))
                .ExecuteUpdate(x => x
                    .SetProperty(m => m.Deleted, true)
                    .SetProperty(m => m.Updated, t));
            AfterDataChange();
        }

        protected virtual void AfterDataChange() { }
        protected virtual void BeforeDataChange(DateTime time) { }
    }
}
