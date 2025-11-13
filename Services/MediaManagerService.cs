using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Models.Interfaces;
using System.Text.Json;

namespace OpenOrderSystem.Core.Services
{
    public class MediaManagerService
    {
        private const string DESCRIPTION_FILE = "__mediaInfo.json";
        private Dictionary<string, string> _mediaDescriptions;
        private bool _grouped = false;

        public MediaManagerService()
        {
            var filepath = Path.Combine(MediaRootPath, DESCRIPTION_FILE);
            if (File.Exists(filepath))
            {
                var json = File.ReadAllText(filepath);
                _mediaDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
            }
            else _mediaDescriptions = new Dictionary<string, string>();
        }

        /// <summary>
        /// Absolute path to the media directory
        /// </summary>
        public static string MediaRootPath { get; set; } = string.Empty;

        /// <summary>
        /// Stores default media for each given media category
        /// </summary>
        public static Dictionary<string, IMedia> DefaultMedia { get; private set; } = new Dictionary<string, IMedia>();

        /// <summary>
        /// Allows mapping backend media type names to frontend user friendly ones
        /// </summary>
        public Dictionary<string, string> DisplayNameMap { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Stores media by type/directory
        /// </summary>
        public Dictionary<string, List<IMedia>> Media { get; private set; } = new Dictionary<string, List<IMedia>>();

        /// <summary>
        /// Retrieve the associated media types
        /// </summary>
        /// <param name="mediaTypes">comma separated list of media types or null to fetch all</param>
        /// <param name="groupSubfolders">true to group all media in a subfolder (default: false)</param>
        /// <returns>Modified MediaManagerService</returns>
        public MediaManagerService FetchMedia(string? mediaTypes = null, bool groupSubfolders = false)
        {
            //ensures media directory always exists
            if (!Directory.Exists(MediaRootPath))
                Directory.CreateDirectory(MediaRootPath);

            var dirMap = new List<string>();

        //media type not specified
        FailedTypeParse:
            if (mediaTypes == null)
            {
                var subDir = Directory.GetDirectories(MediaRootPath);
                dirMap.AddRange(subDir);

                for (int i = 0; i < subDir.Length; i++)
                {
                    //recursively map all sub directories
                    dirMap.AddRange(Directory.GetDirectories(subDir[i]));
                }
            }
            else
            {
                var selections = mediaTypes.Split(',');
                if (selections.Length > 0)
                {
                    foreach (var selection in selections)
                    {
                        var path = Path.Combine(MediaRootPath, selection.Replace('.', '\\'));
                        dirMap.Add(path);
                        for (int i = 0; i < dirMap.Count; ++i)
                        {
                            var subDir = Directory.GetDirectories(dirMap[i]);
                            dirMap.AddRange(subDir);
                        }
                    }
                }
                else //in the event of an empty string it will be treated as a null string.
                {
                    mediaTypes = null;
                    goto FailedTypeParse;
                }
            }

            for (int i = 0; i < dirMap.Count; i++)
            {
                //remap to relative path
                var rel = dirMap[i].Replace(MediaRootPath, "").Replace('\\', '.').Remove(0, 1);
                dirMap[i] = Path.Combine("media", rel);
            }

            dirMap.Sort();

            if (groupSubfolders)
            {
                _grouped = true;
                var rootFolders = dirMap
                    .AsQueryable()
                    .Where(d => !d.Contains('.'));

                foreach (var dir in rootFolders)
                {
                    var path = Path.Combine(MediaRootPath, dir.Replace("media\\", ""));
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    var iMedia = new List<IMedia>();

                    foreach (var file in files)
                    {
                        if (MediaFactory.CanFormat(file))
                        {
                            _mediaDescriptions.TryGetValue(file, out var description);
                            iMedia.Add(MediaFactory.Format(file, description));
                        }
                    }

                    Media[dir.Replace("media\\", "")] = iMedia;
                }
            }
            else
            {
                foreach (var dir in dirMap)
                {
                    var path = Path.Combine(MediaRootPath, dir.Replace('.', '\\').Replace("media\\", ""));
                    var files = Directory.GetFiles(path);
                    var iMedia = new List<IMedia>();

                    foreach (var file in files)
                    {
                        _mediaDescriptions.TryGetValue(file, out var description);
                        iMedia.Add(MediaFactory.Format(file, description));
                    }

                    Media[dir.Replace("media\\", "")] = iMedia;
                }
            }

            return this;
        }

        /// <summary>
        /// Fetches all media of a given type
        /// </summary>
        /// <param name="mediaType">type of media to fetch</param>
        /// <returns>Array containing all the media of the requested type</returns>
        public IMedia[] GetMedia(string mediaType)
        {
            var media = new List<IMedia>();

            if (Media.ContainsKey(mediaType))
            {
                media.AddRange(Media[mediaType]);
            }

            return media.ToArray();
        }

        /// <summary>
        /// Fetches a single media file
        /// </summary>
        /// <param name="mediaType">Type of media to search</param>
        /// <param name="name">Name of the media file</param>
        /// <returns></returns>
        public IMedia? GetMedia(string mediaType, string name)
        {
            IMedia? media = null;

            if (Media.ContainsKey(mediaType))
            {
                //find the media file if available
                media = Media[mediaType].FirstOrDefault(m => m.Name == name);

                //return default if available and media was not found
                if (media == null && DefaultMedia.ContainsKey(mediaType))
                {
                    media = DefaultMedia[mediaType];
                }
            }

            return media;
        }

        public MediaManagerService SetDisplayName(string mediaType, string displayName)
        {
            DisplayNameMap[mediaType] = displayName;

            return this;
        }

        public MediaManagerService DescribeMedia(IMedia media, string description)
        {
            _mediaDescriptions[media.FullPath] = description;
            UpdateDescriptionFile();
            return this;
        }

        public MediaManagerService DescribeMedia(string mediaType, string name, string description)
        {
            var media = GetMedia(mediaType, name);
            if (media != null) DescribeMedia(media, description);

            return this;
        }

        public MediaManagerService UploadFile(IFormFile file, string? description = null)
        {
            string filepath;
            string directory;
            var extension = Path.GetExtension(file.FileName);

            if (MediaFactory.MediaExtensionMap.ContainsKey(extension) && MediaFactory.MediaExtensionMap[extension] == typeof(ImageMedia))
            {
                directory = Path.Combine(MediaRootPath, "images", "user");
            }
            else if (MediaFactory.MediaExtensionMap.ContainsKey(extension) && MediaFactory.MediaExtensionMap[extension] == typeof(AudioMedia))
            {
                directory = Path.Combine(MediaRootPath, "images", "user");
            }
            else
            {
                directory = Path.Combine(MediaRootPath, "unknownUserMedia");
            }

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            filepath = Path.Combine(directory, Path.GetFileName(file.FileName));

            var exists = File.Exists(filepath);
            for (int i = 1; exists; ++i)
            {
                var newFilename = $"{Path.GetFileName(file.FileName).Replace(extension, "")}({i}){extension}";
                filepath = Path.Combine(directory, newFilename);
                exists = File.Exists(filepath);
            }

            using (var stream = File.Open(filepath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            if (File.Exists(filepath))
            {
                var media = MediaFactory.Format(filepath, description);
                if (media != null)
                {
                    var mediaType = media.Path
                        .Replace($"{MediaRootPath}\\", "")
                        .Replace($"{media.Name}{media.Extension}", "")
                        .Replace('\\', '.');

                    if (_grouped)
                    {
                        var dir = mediaType.Split('.');
                        if (dir.Length > 0)
                            mediaType = dir[0];
                    }

                    if (Media.ContainsKey(mediaType))
                        Media[mediaType].Add(media);

                    if (description != null)
                        _mediaDescriptions[description] = description;
                }
            }

            UpdateDescriptionFile();

            return this;
        }

        public MediaManagerService DeleteFile(IMedia media)
        {
            if (File.Exists(media.FullPath))
            {
                //remove file
                File.Delete(media.FullPath);

                //remove any associated descriptions
                if (_mediaDescriptions.ContainsKey(media.FullPath))
                    _mediaDescriptions.Remove(media.FullPath);

                UpdateDescriptionFile();
            }

            return this;
        }

        public MediaManagerService DeleteFile(string mediaType, string name)
        {
            var media = GetMedia(mediaType, name);

            if (media != null)
                DeleteFile(media);

            return this;
        }

        public string GetDisplayName(string mediaType)
        {
            string displayName = mediaType.Replace('.', ' ');

            if (DisplayNameMap.ContainsKey(mediaType))
                displayName = DisplayNameMap[mediaType];

            return displayName;
        }

        private void UpdateDescriptionFile()
        {
            var filepath = Path.Combine(MediaRootPath, DESCRIPTION_FILE);
            var json = JsonSerializer.Serialize(_mediaDescriptions);
            File.WriteAllText(filepath, json);
        }
    }
}
