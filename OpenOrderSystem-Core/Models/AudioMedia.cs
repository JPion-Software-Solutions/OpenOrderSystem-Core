using Microsoft.AspNetCore.Html;
using OpenOrderSystem.Core.Models.Interfaces;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core.Models
{
    public class AudioMedia : IMedia
    {
        public static IMedia Create(string filepath, string? description)
        {
            return new AudioMedia
            {
                FullPath = filepath,
                Description = description
            };
        }

        private string _fullpath = string.Empty;
        public string Name { get; private set; } = string.Empty;

        public string Extension { get; private set; } = string.Empty;

        public string? Description { get; set; } = null;

        public string FullPath
        {
            get => _fullpath;
            set
            {
                _fullpath = value;
                Extension = System.IO.Path.GetExtension(_fullpath);
                Name = System.IO.Path.GetFileName(_fullpath).Replace(Extension, "");
                Path = _fullpath.Replace(MediaManagerService.MediaRootPath, "");
            }
        }

        public string Path { get; private set; } = string.Empty;

        public HtmlString GetHtml(string? id = null, string? classes = null, string? additionalAttr = null)
        {
            id = id == null ? "" : $" id=\"{id}\"";
            classes = classes == null ? "" : $" class=\"{classes}\"";
            additionalAttr = additionalAttr == null ? "" : additionalAttr;

            var html = $"<audio><source src=\"{Path}\" type=\"audio\" /></audio>";
            return new HtmlString(html);
        }
    }
}
