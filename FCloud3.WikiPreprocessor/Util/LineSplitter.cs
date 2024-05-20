﻿using Ganss.Xss;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.WikiPreprocessor.Util
{
    public class LineSplitter
    {
        private static Lazy<HtmlSanitizer> Sanitizer = new(() =>
        {
            var s = new HtmlSanitizer();
            s.AllowedAttributes.Add("class");
            s.AllowedTags.Add("style");
            return s;
        });
        public static List<LineAndHash> Split(string input, ILocatorHash? locatorHash)
        {
            List<string> lines;
            int start = 0;
            int length = 0;
            lines = new();
            int layer = 0;
            foreach (var c in input)
            {
                if (c == Consts.tplt_L)
                    layer++;
                if (c == Consts.tplt_R)
                    layer--;
                if (c == Consts.lineSep && layer == 0)
                {
                    lines.Add(input.Substring(start, length));
                    start += length + 1;
                    length = 0;
                }
                else
                    length++;
            }
            lines.Add(input.Substring(start));

            var hashedLines = lines.ConvertAll(x =>
            {
                x = x.Trim();
                string? hash = locatorHash?.Hash(x);
                return new LineAndHash(hash, Sanitizer.Value.Sanitize(x));
            });
            hashedLines.RemoveAll(line => string.IsNullOrWhiteSpace(line.Text));
            return hashedLines;
        }
    }
    public class LineAndHash
    {
        public string? RawLineHash { get; }
        public string Text { get; set; }
        public LineAndHash(string? rawLineHash, string content)
        {
            RawLineHash = rawLineHash;
            Text = content;
        }
    }
}