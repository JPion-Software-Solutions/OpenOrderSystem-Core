using Microsoft.CodeAnalysis.CSharp.Syntax;
using OpenOrderSystem.Core.Models.Interfaces;
using System.Reflection;

namespace OpenOrderSystem.Core.Models
{
    public class MediaFactory
    {
        public static Dictionary<string, Type> MediaExtensionMap { get; private set; } = new Dictionary<string, Type>
        {
            { ".jpg", typeof(ImageMedia) },
            { ".jpeg", typeof(ImageMedia) },
            { ".png", typeof(ImageMedia) },
            { ".apng", typeof(ImageMedia) },
            { ".bmp", typeof(ImageMedia) },
            { ".ico", typeof(ImageMedia) },
            { ".svg", typeof(ImageMedia) },
            { ".tiff", typeof(ImageMedia) },
            { ".tif", typeof(ImageMedia) },
            { ".gif", typeof(ImageMedia) },
            { ".avif", typeof(ImageMedia) },
            { ".jfif", typeof(ImageMedia) },
            { ".pjpeg", typeof(ImageMedia) },
            { ".pjp", typeof(ImageMedia) },
            { ".cur", typeof(ImageMedia) },
            { ".webp", typeof(ImageMedia) }
        };

        public static bool CanFormat(string filepath)
        {
            var fileExists = File.Exists(filepath);
            var hasMappableExtension = MediaExtensionMap.ContainsKey(Path.GetExtension(filepath));

            return fileExists && hasMappableExtension;
        }

        public static IMedia Format(string filepath, string? description = null)
        {
            var extension = Path.GetExtension(filepath);

            if (MediaExtensionMap.ContainsKey(extension) && MediaExtensionMap[extension] == typeof(ImageMedia))
                return ImageMedia.Create(filepath, description);
            else
                return UnknownMedia.Create(filepath, description);
        }
    }
}
