﻿using FCloud3.App.Services.Filters;
using FCloud3.App.Utils;
using FCloud3.Entities.Identities;
using FCloud3.Repos.Etc.Index;
using FCloud3.Services.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCloud3.App.Controllers.Files
{
    [Authorize]
    public class MaterialController : Controller, IAuthGrantTypeProvidedController
    {
        private readonly MaterialService _materialService;
        public AuthGrantOn AuthGrantOnType => AuthGrantOn.Material;

        public MaterialController(MaterialService materialService)
        {
            _materialService = materialService;
        }
        public IActionResult Index([FromBody]IndexQuery query, bool onlyMine)
        {
            return this.ApiResp(_materialService.Index(query, onlyMine));
        }
        [UserTypeRestricted]
        public IActionResult Add(string name, string? desc, IFormFile content)
        {
            if (content is null)
                return BadRequest();
            using Stream stream = content.OpenReadStream();
            var createdId = _materialService.Add(stream, content.FileName, ValidFilePathBases.material, name, desc, out string? errmsg);
            if (createdId == 0)
                return this.ApiFailedResp(errmsg);
            return this.ApiResp(createdId);
        }
        [AuthGranted(nameof(id))]
        [UserTypeRestricted]
        public IActionResult EditContent(int id, IFormFile content)
        {
            if (content is null)
                return BadRequest();
            if (content.Length > 3 * 1024 * 1024)
                return this.ApiFailedResp("尺寸请勿超过3MB");
            using Stream stream = content.OpenReadStream();
            if (!_materialService.UpdateContent(id, stream, content.FileName, ValidFilePathBases.material, out string? errmsg))
                return this.ApiFailedResp(errmsg);
            return this.ApiResp();
        }
        [AuthGranted(nameof(id))]
        [UserTypeRestricted]
        public IActionResult EditInfo(int id, string name, string? desc)
        {
            if (!_materialService.UpdateInfo(id, name, desc, out string? errmsg))
                return this.ApiFailedResp(errmsg);
            return this.ApiResp();
        }
        [AuthGranted]
        [UserTypeRestricted]
        public IActionResult Delete(int id)
        {
            if (!_materialService.Delete(id, out string? errmsg))
                return this.ApiFailedResp(errmsg);
            return this.ApiResp();
        }
    }
}
