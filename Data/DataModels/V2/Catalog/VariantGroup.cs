using System;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class VariantGroup: IGroupable<VariantGroup, Variant>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public VariantGroup? Parent { get; set; }
    public ICollection<VariantGroup>? Children { get; set; }
    public  ICollection<Variant>? Members { get; set; }
    public int SortPriority { get; set; }
}
