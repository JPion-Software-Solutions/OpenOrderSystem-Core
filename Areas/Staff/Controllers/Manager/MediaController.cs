using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Media;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Authorize(Roles = "admin, manager")]
    [Route("Staff/Manager/Media/{action=Index}")]
    public class MediaController : Controller
    {
        private readonly MediaManagerService _mediaService;
        private readonly ILogger<MediaController> _logger;

        public MediaController(MediaManagerService mediaService, ILogger<MediaController> logger)
        {
            _mediaService = mediaService.FetchMedia();
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [DisableRequestSizeLimit]
        public IActionResult Upload(IFormFile file)
        {
            if (file == null) return NoContent();

            _mediaService.UploadFile(file);

            return RedirectToActionPermanent("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string mediaType, string name)
        {
            _mediaService.DeleteFile(mediaType, name);
            return RedirectToActionPermanent("Index");
        }

        [HttpGet]
        public IActionResult Edit(string mediaType, string name)
        {

            var media = _mediaService
                .FetchMedia(mediaType)
                .GetMedia(mediaType, name);

            var model = new EditVM
            {
                MediaType = mediaType,
                Name = name,
                Description = media?.Description ?? ""
            };

            return PartialView("_EditModal", model);
        }

        [HttpPost]
        public IActionResult Edit(EditVM model)
        {
            if (ModelState.IsValid)
            {
                var media = _mediaService
                    .FetchMedia(model.MediaType)
                    .GetMedia(model.MediaType, model.Name);

                if (media != null)
                {
                    _mediaService.DescribeMedia(media, model.Description);
                }
            }

            return RedirectToActionPermanent("Index");
        }
    }
}
