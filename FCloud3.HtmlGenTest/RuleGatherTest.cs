﻿using FCloud3.HtmlGen.Options;
using FCloud3.HtmlGen.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCloud3.HtmlGenTest
{
    [TestClass]
    public class RuleGatherTest
    {
        [TestMethod]
        [DataRow(
            "123*456*789",
            "*")]
        [DataRow(
            "12**34*5*67**89",
            "**;*")]
        [DataRow(
            "> 123*456*789\n123**456**789",
            "**;*")]
        [DataRow(
            "|12*34*56|78\\bd90\\bd00|\n",
            "*;\\bd")]
        public void Inline(string input,string answerStr)
        {
            var parser = new ParserBuilder().Cache.SwitchToExclusiveCache().BuildParser();
            var element = parser.RunToRaw(input);
            var rules = element.ContainRules()??new();
            var inlineRules = rules.ConvertAll(x=>
            {
                if(x is IInlineRule ir)
                    return ir.MarkLeft;
                return null;
            });
            inlineRules.RemoveAll(x => x is null);
            var answers = answerStr.Split(';').ToList();
            CollectionAssert.AreEquivalent(answers,inlineRules);
        }
    }
}
