﻿using FCloud3.App.Utils;
using FCloud3.Services.Files;
using Microsoft.AspNetCore.Mvc;

namespace FCloud3.App.Controllers.Files
{
    public class FileItemController : Controller
    {
        private readonly FileItemService _fileService;
        private const int maxUploadLength = 10 * 1000 * 1000;

        public FileItemController(FileItemService fileService)
        {
            _fileService = fileService;
        }

        public IActionResult GetDetail(int id)
        {
            var detail = _fileService.GetDetail(id, out string? errmsg);
            if (detail is null)
                return this.ApiFailedResp(errmsg);
            return this.ApiResp(detail);
        }
        public IActionResult Save(FileUploadRequest request)
        {
            if (request is null || request.ToSave is null)
                return BadRequest();
            if (request.StorePath is null || !ValidFilePathBases.Contains(request.StorePath))
                return this.ApiFailedResp("不支持的StorePath");
            if (request.DisplayName is null)
                return this.ApiFailedResp("请填写文件显示名");
            if (request.ToSave.Length > maxUploadLength)
                return this.ApiFailedResp("文件过大，请压缩或分开上传");
            int id = _fileService.Save(
                stream: request.ToSave.OpenReadStream(),
                byteCount: (int)request.ToSave.Length,
                displayName: request.DisplayName,
                storePath: request.StorePath,
                storeName: null, out string? errmsg);
            if (id <= 0)
                return this.ApiFailedResp(errmsg);
            return this.ApiResp(new {CreatedId = id});
        }

        public class FileUploadRequest
        {
            public IFormFile? ToSave { get; set; }
            public string? DisplayName { get; set; }
            public string? StorePath { get; set; }
            public string? StoreName { get; set; }
        }
    }
}
