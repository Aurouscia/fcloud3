﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.HtmlGen.Options
{
    public interface IHtmlGenOptions
    {
        public void OverrideWith(IHtmlGenOptions another);
    }
}