using Aurouscia.TableEditor.Core;
using FCloud3.DbContexts;
using FCloud3.Entities.Table;
using FCloud3.Entities.Wiki;
using FCloud3.Repos.Etc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FCloud3.Repos.Table
{
    public class FreeTableRepo:RepoBase<FreeTable>
    {
        private const int briefCellMaxStrLength = 12;
        private const string newTableDefaultName = "新建表格";
        public FreeTableRepo(FCloudContext context, ICommitingUserIdProvider userIdProvider) : base(context, userIdProvider)
        {
        }

        public List<FreeTableMeta> GetMetaRangeByIds(List<int> ids)
        {
            if (ids.Count == 0)
                return new();
            return base.GetRangeByIds(ids).GetMeta().ToList();
        }
        
        public List<FreeTable> Search(string str)
        {
            return Existing.Where(x => x.Data != null && x.Data.Contains(str))
                .OrderByDescending(x => x.Updated).Take(10).ToList();
        }

        public IQueryable<FreeTable> SearchCopyableName(string str)
        {
            return Existing
                .Where(x => x.Name != null
                    && x.Name.Contains(str)
                    && x.Name.Contains(WikiPara.copyableMark))
                .OrderBy(x => x.Name);
        }
        
        public bool TryEditInfo(int id, string name, out string? errmsg)
        {
            name ??= "";
            if (!NameCheck(name, out errmsg)) {
                return false;
            }
            int affected = Existing.Where(x => x.Id == id)
                .ExecuteUpdate(x => x
                    .SetProperty(t => t.Name, name)
                    .SetProperty(t => t.Updated, DateTime.Now)
                );
            AfterDataChange();
            return affected == 1;
        }

        public bool TryEditContent(FreeTable model, AuTable data, string dataRaw, out string? errmsg)
        {
            var brief = Brief(data.Cells);
            string briefJson = brief.ToJsonStr();
            if (model is null)
            {
                errmsg = "找不到指定表格";
                return false;
            }
            model.Brief = briefJson;
            model.Data = dataRaw;
            base.Update(model);
            errmsg = null;
            return true;
        }

        public int TryCreateWithContent(AuTable data, string name, out string? errmsg)
        {
            if (!NameCheck(name, out errmsg))
                return 0;
            var brief = Brief(data.Cells);
            string briefJson = brief.ToJsonStr();
            var model = new FreeTable();
            model.Name = name;
            model.Brief = briefJson;
            model.Data = FreeTableDataConvert.Serialize(data);
            return base.AddAndGetId(model);
        }
        public bool StageCreateWithContent(AuTable data, string name, out FreeTable? model, out string? errmsg)
        {
            if (!NameCheck(name, out errmsg))
            {
                model = null;
                return false;
            }
            var brief = Brief(data.Cells);
            model = new FreeTable();
            model.Name = name;
            model.SetBrief(brief);
            model.SetData(data);
            base.BatchPrepare([model]);
            errmsg = null;
            return true;
        }
        private FreeTableBrief Brief(List<List<string?>?>? cells)
        {
            FreeTableBrief brief = new();
            if(cells is null)
            {
                return DefaultBrief();
            }
            for (int r = 0; r < cells.Count && r < 3; r++)
            {
                var row = cells[r];
                if (row is null) continue;
                var briefRow = new List<string>();
                brief.Add(briefRow);
                for (int c = 0; c < row.Count && c < 4; c++)
                {
                    string cellVal = row[c] ?? "";
                    if (cellVal.Length > briefCellMaxStrLength)
                        cellVal = string.Concat(cellVal.AsSpan(0, briefCellMaxStrLength - 1), "...");
                    briefRow.Add(cellVal);
                }
            }
            return brief;
        }
        private static List<List<string?>?> DefaultCells()
        {
            return new List<List<string?>?>()
                {
                    new List<string?>(){"",""},
                    new List<string?>(){"",""}
                };
        }
        private static FreeTableBrief DefaultBrief()
        {
            return new()
                {
                    new List<string>(){"",""},
                    new List<string>(){"",""}
                };
        }

        public override FreeTable NewDefaultObject()
        {
            AuTable table = new()
            {
                Name = "",
                Cells = DefaultCells(),
                Merges = new()
            };
            var brief = DefaultBrief();
            FreeTable creating = new();
            creating.SetData(table);
            creating.SetBrief(brief);
            creating.Name = "";
            return creating;
        }

        public int TryCopyAndGetId(int copySrc, out string? errmsg)
        {
            var target = GetById(copySrc) ?? throw new Exception("找不到复制目标");
            if (target.Name is null
                || !target.Name.Contains(WikiPara.copyableMark))
            {
                errmsg = "不可复制";
                return 0;
            }
            var nameWithoutMark = target.Name.Replace(WikiPara.copyableMark, "");
            FreeTable copied = new()
            {
                Name = nameWithoutMark,
                Data = target.Data,
                Brief = target.Brief
            };
            errmsg = null;
            return base.AddAndGetId(copied);
        }

        private static bool NameCheck(string name, out string? errmsg)
        {
            if (name.Length > 25)
            {
                errmsg = "表格名称过长，请缩短";
                return false;
            }
            errmsg = null;
            return true;
        }
    }


    public class FreeTableMeta
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Brief { get; set; }

        public int CreatorUserId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool Deleted { get; set; }
    }
    public static class FreeTableMetaQuerier
    {
        public static IQueryable<FreeTableMeta> GetMeta(this IQueryable<FreeTable> freeTables)
        {
            return freeTables.Select(data => new FreeTableMeta()
            {
                Id = data.Id,
                Name = data.Name,
                Brief = data.Brief,
                CreatorUserId = data.CreatorUserId,
                Created = data.Created,
                Updated = data.Updated,
                Deleted = data.Deleted,
            });
        }
    }

    public class FreeTableBrief: List<List<string>>
    {
    }

    public static class FreeTableDataConvert
    {
        public static AuTable Deserialize(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return new();
            return JsonConvert.DeserializeObject<AuTable>(data, jsonSettings) ?? new();
        }
        public static string Serialize(AuTable table)
        {
            return JsonConvert.SerializeObject(table, jsonSettings);
        }
        public static AuTable GetData(this FreeTable table) 
        {
            return Deserialize(table.Data);
        }
        public static void SetData(this FreeTable table, AuTable tableData)
        {
            string dataStr = JsonConvert.SerializeObject(tableData, jsonSettings);
            table.Data = dataStr;
        }
        public static void SetBrief(this FreeTable table, FreeTableBrief briefData)
        {
            string briefStr = briefData.ToJsonStr();
            table.Brief = briefStr;
        }
        public static string ToJsonStr(this FreeTableBrief briefData)
        {
            return JsonConvert.SerializeObject(briefData, jsonSettings);
        }

        private static readonly JsonSerializerSettings jsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
