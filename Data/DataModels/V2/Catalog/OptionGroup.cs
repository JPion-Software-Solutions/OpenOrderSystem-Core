using System;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class OptionGroup : IGroupable<OptionGroup, Option>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public OptionGroup? Parent { get; set; }
    public ICollection<OptionGroup>? Children { get; set; }
    public int SortPriority { get; set; }
    public ICollection<Option>? Members { get; set;}
}
