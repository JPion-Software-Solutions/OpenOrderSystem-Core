using System;
using OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

public class ProductGroup : IGroupable<ProductGroup, Product>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortPriority { get; set; }
    public Guid? ParentId { get; set; }
    public ProductGroup? Parent { get; set; }
    public ICollection<ProductGroup>? Children { get; set; }
    public ICollection<Product>? Members { get; set;}
}
