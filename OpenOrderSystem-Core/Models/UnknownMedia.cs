using Microsoft.AspNetCore.Html;
using OpenOrderSystem.Core.Models.Interfaces;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Models;

namespace OpenOrderSystem.Core.Models
{
    public class UnknownMedia : IMedia
    {
        public static IMedia Create(string filepath, string? description)
        {
            return new ImageMedia
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
            return new HtmlString("<p>error loading unknown media format</p>");
        }
    }
}
