using System;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class MediaGroup : IGroupable<MediaGroup, Media>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public MediaGroup? Parent { get; set; }
    public ICollection<MediaGroup>? Children { get; set; }
    public ICollection<Media>? Members { get; set; }
    public int SortPriority { get; set; }
}
