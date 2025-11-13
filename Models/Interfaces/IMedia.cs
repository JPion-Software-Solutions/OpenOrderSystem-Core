using Microsoft.AspNetCore.Html;

namespace OpenOrderSystem.Core.Models.Interfaces
{
    public interface IMedia
    {
        /// <summary>
        /// Name of the media file
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// File extension of the media
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Description of the media
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// absolute path to the file on the current system
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// relative path to the file
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Get html element(s) for embedding media
        /// </summary>
        /// <param name="id">html id associated with media</param>
        /// <param name="classes">classes applied to media</param>
        /// <param name="additionalAttr">any additional attributes applied to media</param>
        /// <returns>HtmlString containing the media file</returns>
        public HtmlString GetHtml(string? id = null, string? classes = null, string? additionalAttr = null);

        public static virtual IMedia Create(string filepath, string? description)
        {
            return UnknownMedia.Create(filepath, description);
        }
    }
}
