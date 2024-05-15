﻿using FCloud3.Services.TextSec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FCloud3.WikiPreprocessor.Mechanics;
using FCloud3.Entities.TextSection;
using FCloud3.Services.WikiParsing.Support;
using FCloud3.WikiPreprocessor.Util;
using FCloud3.Entities.Wiki;
using FCloud3.Services.Wiki;
using FCloud3.App.Services.Filters;
using FCloud3.Entities.Identities;
using FCloud3.App.Models.COM;

namespace FCloud3.App.Controllers.TextSec
{
    [Authorize]
    public class TextSectionController:Controller, IAuthGrantTypeProvidedController
    {
        private readonly TextSectionService _textSectionService;
        private readonly WikiParserProviderService _genParser;
        private readonly WikiTitleContainService _titleContainService;
        private readonly WikiParaService _wikiParaService;
        private readonly ILocatorHash _locatorHash;
        public AuthGrantOn AuthGrantOnType => AuthGrantOn.TextSection;

        public TextSectionController(
            TextSectionService textSectionService,
            WikiParserProviderService genParser,
            WikiTitleContainService titleContainService,
            WikiParaService wikiParaService,
            ILocatorHash locatorHash) 
        {
            _textSectionService = textSectionService;
            _genParser = genParser;
            _titleContainService = titleContainService;
            _wikiParaService = wikiParaService;
            _locatorHash = locatorHash;
        }

        [AuthGranted(AuthGrantOn.WikiPara)]
        public IActionResult CreateForPara(int paraId)
        {
            int createdId = _textSectionService.TryAddAndAttach(paraId, out string? errmsg);
            if (createdId <= 0)
                return this.ApiFailedResp(errmsg);
            return this.ApiResp(new { CreatedId = createdId });
        }

        [AuthGranted]
        [UserTypeRestricted]
        public IActionResult Edit(int id)
        {
            var res = _textSectionService.GetForEditing(id, out string? errmsg);
            if(errmsg is not null || res is null)
                return this.ApiFailedResp(errmsg);
            TextSectionComModel model = new(res);
            return this.ApiResp(model);
        }
        [AuthGranted]
        [UserTypeRestricted]
        [UserActiveOperation]
        public IActionResult EditExe([FromBody] TextSectionComModel model)
        {
            if (!_textSectionService.TryUpdate(model.Id, model.Title, model.Content, out string? errmsg))
                return this.ApiFailedResp(errmsg);
            return this.ApiResp();
        }

        [AuthGranted(nameof(id))]
        [UserTypeRestricted]
        public IActionResult Preview(int id, string content)
        {
            string cacheKey = $"tse_{id}";
            List<WikiTitleContain> contains = _titleContainService.GetByTypeAndObjId(WikiTitleContainType.TextSection, id);
            var parser = _genParser.Get(cacheKey, builder =>
            {
                builder.UseLocatorHash(_locatorHash);
                builder.EnableDebugInfo();
                builder.ClearUsageInfoOnCall();
            },
            contains,
            false,
            () => _wikiParaService.WikiContainingIt(WikiParaType.Text, id).ToArray()
            );
            var res = new TextSectionPreviewResponse(parser.RunToParserResult(content));
            return this.ApiResp(res);
        }


        public class TextSectionComModel:IAuthGrantableRequestModel
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public int AuthGrantOnId => Id;

            public TextSectionComModel() { }
            public TextSectionComModel(TextSection original)
            {
                Id = original.Id;
                Title = original.Title;
                Content = original.Content;
            }
        }
        public class TextSectionPreviewResponse
        {
            public string HtmlSource { get; }
            public string PreScripts { get; }
            public string PostScripts { get; }
            public string Styles { get; }
            public TextSectionPreviewResponse(string htmlSource)
            {
                HtmlSource = htmlSource;
                PreScripts = "";
                PostScripts = "";
                Styles = "";
            }
            public TextSectionPreviewResponse(ParserResult parserResult)
            {
                HtmlSource = parserResult.Content + parserResult.FootNotes;
                PreScripts = parserResult.PreScript;
                PostScripts = parserResult.PostScript;
                Styles = parserResult.Style;
            }
        }
    }
}
