﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.Entities.Wiki
{
    public class WikiToDir:IDbModel, IRelation
    {
        public int Id { get; set; }
        public int DirId { get; set; }
        public int WikiId { get; set; }

        public int Order { get; set; }

        public int CreatorUserId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool Deleted { get; set; }

        public int RelationMainId => DirId;
        public int RelationSubId=> WikiId;
        public override string ToString()
        {
            return $"{DirId}-{WikiId}";
        }
    }
}
