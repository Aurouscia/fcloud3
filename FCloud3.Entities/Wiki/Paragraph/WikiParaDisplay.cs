﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.Entities.Wiki.Paragraph
{
    public class WikiParaDisplay
    {
        /// <summary>
        /// 指该段落Id
        /// </summary>
        public int ParaId { get; set; }
        /// <summary>
        /// 指该段落代表的文本段/文件/表格的Id
        /// </summary>
        public int UnderlyingId { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public int Order { get; set; }
        public WikiParaType Type { get; set; }
        public WikiParaDisplay(WikiPara para, int underlyingId, string? title, string? content, WikiParaType type)
        {
            ParaId = para.Id;
            Order = para.Order;
            UnderlyingId = underlyingId;
            Title = title;
            Content = content;
            Type = type;
        }
    }
    public static class WikiParaDisplayListConverter
    {
        public static List<WikiParaDisplay> ToDisplaySimpleList(this List<KeyValuePair<WikiPara, IWikiParaObject>> para)
        {
            return para.ConvertAll(x =>
            {
                WikiPara para = x.Key;
                IWikiParaObject paraEntity = x.Value;
                WikiParaDisplay display = paraEntity.ToDisplaySimple(para);
                return display;
            });
        }
    }
    public class WikiParaPlaceholder : IWikiParaObject
    {
        public WikiParaType Type { get; set; }
        private string _title;
        public WikiParaPlaceholder(WikiParaType type)
        {
            Type = type;
            if (type == WikiParaType.Text)
                _title = "空文本段落";
            else if (type == WikiParaType.File)
                _title = "空文件段落";
            else if (type == WikiParaType.Table)
                _title = "空表格段落";
            else
                _title = "???";
        }

        public WikiParaDisplay ToDisplay(WikiPara para)
        {
            return new WikiParaDisplay(para, 0, _title,"" , Type);
        }

        public WikiParaDisplay ToDisplaySimple(WikiPara para)
        {
            return new WikiParaDisplay(para, 0, _title, "", Type);
        }
    }
}
