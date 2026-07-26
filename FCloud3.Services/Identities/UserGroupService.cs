using FCloud3.Entities.Identities;
using FCloud3.Repos.Identities;
using System.Linq;
using FCloud3.Services.Etc;
using FCloud3.Services.Messages;

namespace FCloud3.Services.Identities
{
    public class UserGroupService
    {
        private readonly UserGroupRepo _userGroupRepo;
        private readonly UserToGroupRepo _userToGroupRepo;
        private readonly UserRepo _userRepo;
        private readonly int _userId;
        private readonly NotificationService _notificationService;

        public UserGroupService(
            IOperatingUserIdProvider userIdProvider,
            UserGroupRepo userGroupRepo,
            UserToGroupRepo userToGroupRepo,
            UserRepo userRepo,
            NotificationService notificationService) 
        {
            _userGroupRepo = userGroupRepo;
            _userToGroupRepo = userToGroupRepo;
            _userRepo = userRepo;
            _userId = userIdProvider.Get();
            _notificationService = notificationService;
        }
        public bool Create(string name, out string? errmsg)
        {
            var g = new UserGroup()
            {
                Name = name,
                OwnerUserId = _userId
            };
            var id = _userGroupRepo.TryAddAndGetId(g, out errmsg);
            if(id == 0)
                return false;
            return AddUserToGroup(_userId, id, false , out errmsg);
        }
        public UserGroup? GetById(int id, out string? errmsg)
        {
            errmsg = null;
            var res = _userGroupRepo.GetById(id);
            if (res is null)
                errmsg = "未能找到指定id的群组";
            return res;
        }
        public UserGroupListResult? GetList(string? searchGroupName, out string? errmsg)
        {
            var list = (from g in _userGroupRepo.Existing
                    from r in _userToGroupRepo.Existing
                    where g.Name != null && g.Name.Contains(searchGroupName??"")
                    where r.GroupId == g.Id
                    group new { r.UserId, r.Type } by new { g.Id, g.Name }
                    ).ToList();
            List<UserGroupListResult.UserGroupListResultItem> invitingMe = new();
            List<UserGroupListResult.UserGroupListResultItem> meIn = new();
            List<UserGroupListResult.UserGroupListResultItem> others = new();
            foreach(var grouping in list)
            {
                var model = new UserGroupListResult.UserGroupListResultItem()
                {
                    Id = grouping.Key.Id,
                    Name = grouping.Key.Name,
                    MemberCount = grouping.Where(x=>x.Type.IsFormalMember()).Count()
                };
                var relatingMe = grouping.FirstOrDefault(x => x.UserId == _userId);
                if (relatingMe is not null)
                {
                    if (relatingMe.Type.IsInviting())
                        invitingMe.Add(model);
                    else if(relatingMe.Type.IsFormalMember())
                        meIn.Add(model);
                }
                else
                    others.Add(model);
            }
            invitingMe.Sort((x, y) => y.MemberCount - x.MemberCount);
            meIn.Sort((x, y) => y.MemberCount - x.MemberCount);
            others.Sort((x, y) => y.MemberCount - x.MemberCount);
            UserGroupListResult result = new()
            {
                InvitingMe = invitingMe,
                MeIn = meIn,
                Others = others
            };
            errmsg = null;
            return result;
        }
        public UserGroupDetailResult? GetDetail(int id, out string? errmsg)
        {
            var group = _userGroupRepo.GetById(id);
            if (group is null) 
            {
                errmsg = "找不到指定id的群组";
                return null; 
            }
            var relations =
                from r in _userToGroupRepo.Existing
                from u in _userRepo.Existing
                where r.GroupId == id
                where r.UserId == u.Id
                select new { u.Id, u.Name, r.Type, u.Updated, r.ShowLabel };
            var inviting = new List<UserGroupDetailResult.UserGroupDetailResultMemberItem>();
            var formalMembers = new List<UserGroupDetailResult.UserGroupDetailResultMemberItem>();
            var meShowItsLabel = false;
            relations.ToList().ForEach(x => {
                if(x.Id == _userId)
                    meShowItsLabel = meShowItsLabel || x.ShowLabel;
                var model = new UserGroupDetailResult.UserGroupDetailResultMemberItem()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Type = x.Type,
                    UserUpdated = x.Updated
                };
                if (x.Type.IsInviting())
                    inviting.Add(model);
                else if (x.Type.IsFormalMember())
                    formalMembers.Add(model);
            });
            var owner = formalMembers.Find(x => x.Id == group.OwnerUserId);
            formalMembers.Sort((x, y) =>
            {
                int xIsOwner = x.Id == group.OwnerUserId ? 1 : 0;
                int yIsOwner = y.Id == group.OwnerUserId ? 1 : 0;
                if (xIsOwner != yIsOwner)
                    return yIsOwner - xIsOwner;
                return DateTime.Compare(y.UserUpdated, x.UserUpdated);
            });
            UserGroupDetailResult res = new()
            {
                Id = id,
                Name = group.Name,
                Owner = owner?.Name,
                CanEdit = group.OwnerUserId == _userId, //TODO：其他获得权限的方式
                CanInvite = group.OwnerUserId == _userId, //TODO：其他获得权限的方式
                IsMember = formalMembers.Any(x => x.Id == _userId),
                MeShowItsLabel = meShowItsLabel,
                FormalMembers = formalMembers,
                Inviting = inviting
            };
            errmsg = null;
            return res;
        }
        public bool EditInfo(int id,string? name,out string? errmsg)
        {
            var g = _userGroupRepo.GetById(id);
            if(g is null)
            {
                errmsg = "找不到指定群组";
                return false;
            }
            g.Name = name;
            return _userGroupRepo.TryUpdate(g, out errmsg);
        }
        public bool SetShowLabel(int id, bool showLabel, out string? errmsg) 
        {
            return _userToGroupRepo.SetShowLabel(_userId, id, showLabel, out errmsg);
        }
        public bool AddUserToGroup(int userId, int groupId, bool needAudit, out string? errmsg)
        {
            var s = _userToGroupRepo.AddUserToGroup(userId, groupId, needAudit, out errmsg);
            if (s && needAudit)
            {
                _notificationService.UserGroupInvite(groupId, userId);
            }
            return s;
        }
        public bool AnswerInvitaion(int groupId, bool accept, out string? errmsg)
        {
            if (accept)
                return _userToGroupRepo.AcceptInvitaion(_userId, groupId, out errmsg);
            else
                return _userToGroupRepo.RejectInvitaion(_userId, groupId, out errmsg);
        }
        public bool RemoveUserFromGroup(int userId, int groupId, out string? errmsg)
        {
            var owner = _userGroupRepo.GetOwnerIdById(groupId);
            if (owner == userId)
            {
                errmsg = "组长禁止移出自己";
                return false;
            }
            return _userToGroupRepo.RemoveUserFromGroup(userId, groupId, out errmsg);
        }
        public bool Leave(int groupId, out string? errmsg)
        {
            var owner = _userGroupRepo.GetOwnerIdById(groupId);
            if(owner == _userId)
            {
                errmsg = "暂不支持群主退出";
                return false;
            }
            return RemoveUserFromGroup(_userId, groupId, out errmsg);
        }
        public bool Dissolve(int id,out string? errmsg)
        {
            var g = _userGroupRepo.GetById(id);
            if (g is null)
            {
                errmsg = "找不到指定群组";
                return false;
            }
            _userToGroupRepo.RemoveGroup(id);
            _userGroupRepo.Remove(g);
            errmsg = null;
            return true;
        }
        public class UserGroupListResult
        {
            public List<UserGroupListResultItem>? InvitingMe { get; set; }
            public List<UserGroupListResultItem>? MeIn { get; set; }
            public List<UserGroupListResultItem>? Others { get; set; }
            public class UserGroupListResultItem
            {
                public int Id { get; set; }
                public string? Name { get; set; }
                public int MemberCount { get; set; }
            }
        }
        public class UserGroupDetailResult
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Owner { get; set; }
            public bool CanEdit { get; set; }
            public bool CanInvite { get; set; }
            public bool IsMember { get; set; }
            public bool MeShowItsLabel { get; set; }
            public List<UserGroupDetailResultMemberItem>? Inviting { get; set; }
            public List<UserGroupDetailResultMemberItem>? FormalMembers { get; set; } 
            public class UserGroupDetailResultMemberItem
            {
                public int Id { get; set; }
                public UserToGroupType Type { get; set; }
                public string? Name { get; set; }
                public DateTime UserUpdated { get; set; }
            }
        }
    }
}
