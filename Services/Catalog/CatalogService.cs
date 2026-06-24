using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
using OpenOrderSystem.Core.Services.Catalog.Dto;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;

namespace OpenOrderSystem.Core.Services.Catalog;

public class CatalogService : ICatalogManager
{
    private readonly IDbContextFactory<OosDbContext> _contextFactory;

    public CatalogService(IDbContextFactory<OosDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ===========================
    // IReadOnlyCatalog — Products
    // ===========================

    public async Task<CatalogProductQueryResult> FindProduct(Guid id, ProductLocatorFlags flags = ProductLocatorFlags.None)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await BuildProductQuery(ctx, flags).FirstOrDefaultAsync(p => p.Id == id);
            if (entity is null)
                return NotFoundProduct(flags);

            var options = await LoadOptionsIfRequested(ctx, id, flags);
            return OkProduct(ToProductDto(entity, options, flags), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    public async Task<CatalogProductQueryResult> FindProduct(string name, ProductLocatorFlags flags = ProductLocatorFlags.None)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await BuildProductQuery(ctx, flags).FirstOrDefaultAsync(p => p.Name == name);
            if (entity is null)
                return NotFoundProduct(flags);

            var options = await LoadOptionsIfRequested(ctx, entity.Id, flags);
            return OkProduct(ToProductDto(entity, options, flags), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    public async Task<CatalogProductQueryResult> FindProducts(Guid groupId, ProductLocatorFlags flags = ProductLocatorFlags.None)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var groupIds = await CollectGroupIds(ctx, groupId, CatalogGroupType.Product, flags);
            var entities = await BuildProductQuery(ctx, flags)
                .Where(p => p.GroupId.HasValue && groupIds.Contains(p.GroupId.Value))
                .ToListAsync();

            return OkProducts(await MapProducts(ctx, entities, flags), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    public async Task<CatalogProductQueryResult> FindProducts(string groupName, ProductLocatorFlags flags = ProductLocatorFlags.None)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var matchingGroupIds = await ctx.ProductGroups
                .Where(g => g.Name == groupName)
                .Select(g => g.Id)
                .ToListAsync();

            if (matchingGroupIds.Count == 0)
                return OkProducts([], flags);

            var groupIds = new HashSet<Guid>();
            foreach (var gid in matchingGroupIds)
                groupIds.UnionWith(await CollectGroupIds(ctx, gid, CatalogGroupType.Product, flags));

            var entities = await BuildProductQuery(ctx, flags)
                .Where(p => p.GroupId.HasValue && groupIds.Contains(p.GroupId.Value))
                .ToListAsync();

            return OkProducts(await MapProducts(ctx, entities, flags), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    public async Task<CatalogProductQueryResult> EnrichProduct(CatalogProductDto product, ProductLocatorFlags flags = ProductLocatorFlags.None)
    {
        if (product.Id is null)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product.Id must be set.", WithFlags = flags };
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await BuildProductQuery(ctx, flags).FirstOrDefaultAsync(p => p.Id == product.Id.Value);
            if (entity is null)
                return NotFoundProduct(flags);

            var options = await LoadOptionsIfRequested(ctx, entity.Id, flags);
            return OkProduct(ToProductDto(entity, options, flags), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    // ===========================
    // IReadOnlyCatalog — Options
    // ===========================

    public async Task<CatalogOptionQueryResult> FindOption(Guid id)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Options.FirstOrDefaultAsync(o => o.Id == id);
            return entity is null
                ? new CatalogOptionQueryResult { Status = CatalogResultStatus.NotFound }
                : new CatalogOptionQueryResult { Results = [ToOptionDto(entity)] };
        }
        catch (Exception ex) { return new CatalogOptionQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogOptionQueryResult> FindOption(string name)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Options.FirstOrDefaultAsync(o => o.Name == name);
            return entity is null
                ? new CatalogOptionQueryResult { Status = CatalogResultStatus.NotFound }
                : new CatalogOptionQueryResult { Results = [ToOptionDto(entity)] };
        }
        catch (Exception ex) { return new CatalogOptionQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogOptionQueryResult> FindOptions(Guid groupId)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entities = await ctx.Options.Where(o => o.GroupId == groupId).ToListAsync();
            return new CatalogOptionQueryResult { Results = entities.Select(ToOptionDto).ToList() };
        }
        catch (Exception ex) { return new CatalogOptionQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogOptionQueryResult> FindOptions(string groupName)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var groupIds = await ctx.OptionGroups.Where(g => g.Name == groupName).Select(g => g.Id).ToListAsync();
            if (groupIds.Count == 0)
                return new CatalogOptionQueryResult();

            var entities = await ctx.Options
                .Where(o => o.GroupId.HasValue && groupIds.Contains(o.GroupId.Value))
                .ToListAsync();
            return new CatalogOptionQueryResult { Results = entities.Select(ToOptionDto).ToList() };
        }
        catch (Exception ex) { return new CatalogOptionQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    // =========================
    // IReadOnlyCatalog — Media
    // =========================

    public async Task<CatalogMediaQueryResult> FindMedia(Guid id)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Media.FirstOrDefaultAsync(m => m.Id == id);
            return entity is null
                ? new CatalogMediaQueryResult { Status = CatalogResultStatus.NotFound }
                : new CatalogMediaQueryResult { Results = [ToMediaDto(entity)] };
        }
        catch (Exception ex) { return new CatalogMediaQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogMediaQueryResult> FindMedia(string name)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Media.FirstOrDefaultAsync(m => m.Name == name);
            return entity is null
                ? new CatalogMediaQueryResult { Status = CatalogResultStatus.NotFound }
                : new CatalogMediaQueryResult { Results = [ToMediaDto(entity)] };
        }
        catch (Exception ex) { return new CatalogMediaQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogMediaQueryResult> FindMediaInGroup(Guid groupId)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entities = await ctx.Media.Where(m => m.GroupId == groupId).ToListAsync();
            return new CatalogMediaQueryResult { Results = entities.Select(ToMediaDto).ToList() };
        }
        catch (Exception ex) { return new CatalogMediaQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    public async Task<CatalogMediaQueryResult> FindMediaInGroup(string groupName)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var groupIds = await ctx.MediaGroups.Where(g => g.Name == groupName).Select(g => g.Id).ToListAsync();
            if (groupIds.Count == 0)
                return new CatalogMediaQueryResult();

            var entities = await ctx.Media
                .Where(m => m.GroupId.HasValue && groupIds.Contains(m.GroupId.Value))
                .ToListAsync();
            return new CatalogMediaQueryResult { Results = entities.Select(ToMediaDto).ToList() };
        }
        catch (Exception ex) { return new CatalogMediaQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
    }

    // ==========================
    // IReadOnlyCatalog — Groups
    // ==========================

    public async Task<CatalogGroupQueryResult> FindGroup(Guid id, CatalogGroupType groupType)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            CatalogGroupDto? dto = groupType switch
            {
                CatalogGroupType.Product => ToGroupDtoOrNull(await ctx.ProductGroups.FirstOrDefaultAsync(g => g.Id == id)),
                CatalogGroupType.Option  => ToGroupDtoOrNull(await ctx.OptionGroups.FirstOrDefaultAsync(g => g.Id == id)),
                CatalogGroupType.Media   => ToGroupDtoOrNull(await ctx.MediaGroups.FirstOrDefaultAsync(g => g.Id == id)),
                _                        => null
            };
            return dto is null
                ? new CatalogGroupQueryResult { Status = CatalogResultStatus.NotFound, GroupType = groupType }
                : new CatalogGroupQueryResult { Results = [dto], GroupType = groupType };
        }
        catch (Exception ex) { return new CatalogGroupQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message, GroupType = groupType }; }
    }

    public async Task<CatalogGroupQueryResult> FindGroups(string name, CatalogGroupType groupType)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            IReadOnlyList<CatalogGroupDto> results = groupType switch
            {
                CatalogGroupType.Product => (await ctx.ProductGroups.Where(g => g.Name == name).ToListAsync()).Select(ToGroupDto).ToList(),
                CatalogGroupType.Option  => (await ctx.OptionGroups.Where(g => g.Name == name).ToListAsync()).Select(ToGroupDto).ToList(),
                CatalogGroupType.Media   => (await ctx.MediaGroups.Where(g => g.Name == name).ToListAsync()).Select(ToGroupDto).ToList(),
                _                        => []
            };
            return new CatalogGroupQueryResult { Results = results, GroupType = groupType };
        }
        catch (Exception ex) { return new CatalogGroupQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message, GroupType = groupType }; }
    }

    // ==========================
    // ICatalogManager — Products
    // ==========================

    public async Task<CatalogProductQueryResult> AddProduct(CatalogProductDto product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product name is required." };
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = new Product
            {
                Name        = product.Name,
                Description = product.Description,
                Keywords    = product.Keywords,
                CoverMediaId = product.CoverMedia?.Id,
                AlbumId     = product.MediaAlbum?.Id,
                GroupId     = product.CatalogProductGroup?.Id,
            };
            if (product.Metadata is not null)
                foreach (var (k, v) in product.Metadata) entity.SetMetadata(k, v);

            ctx.Products.Add(entity);
            await ctx.SaveChangesAsync();
            return OkProduct(ToProductDto(entity, null, ProductLocatorFlags.None));
        }
        catch (Exception ex) { return ErrorProduct(ex); }
    }

    public async Task<CatalogProductQueryResult> UpdateProduct(CatalogProductDto product)
    {
        if (product.Id is null)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product.Id is required for update." };
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == product.Id.Value);
            if (entity is null)
                return NotFoundProduct();

            if (product.Name        is not null) entity.Name        = product.Name;
            if (product.Description is not null) entity.Description = product.Description;
            if (product.Keywords    is not null) entity.Keywords    = product.Keywords;
            if (product.CoverMedia?.Id      is not null) entity.CoverMediaId = product.CoverMedia.Id;
            if (product.MediaAlbum?.Id      is not null) entity.AlbumId      = product.MediaAlbum.Id;
            if (product.CatalogProductGroup?.Id is not null) entity.GroupId  = product.CatalogProductGroup.Id;
            if (product.Metadata is not null)
                foreach (var (k, v) in product.Metadata) entity.SetMetadata(k, v);

            await ctx.SaveChangesAsync();
            return OkProduct(ToProductDto(entity, null, ProductLocatorFlags.None));
        }
        catch (Exception ex) { return ErrorProduct(ex); }
    }

    public async Task<CatalogProductQueryResult> DeleteProduct(CatalogProductDto product)
    {
        if (product.Id is null)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product.Id is required for delete." };
        return await DeleteProduct(product.Id.Value);
    }

    public async Task<CatalogProductQueryResult> DeleteProduct(Guid productId)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (entity is null)
                return NotFoundProduct();

            var snapshot = ToProductDto(entity, null, ProductLocatorFlags.None);
            ctx.Products.Remove(entity);
            await ctx.SaveChangesAsync();
            return OkProduct(snapshot);
        }
        catch (Exception ex) { return ErrorProduct(ex); }
    }

    // ==========================
    // ICatalogManager — Options
    // ==========================

    public async Task<CatalogResult> AddOption(CatalogOptionDto option)
    {
        if (string.IsNullOrWhiteSpace(option.Name))
            return Fail(CatalogResultStatus.ValidationError, "Option name is required.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            ctx.Options.Add(new Option
            {
                Name       = option.Name,
                PriceDelta = option.PriceDelta ?? 0m,
                Flags      = option.Flags      ?? OptionFlags.None,
                GroupId    = option.GroupId,
            });
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> UpdateOption(CatalogOptionDto option)
    {
        if (option.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Option.Id is required for update.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Options.FirstOrDefaultAsync(o => o.Id == option.Id.Value);
            if (entity is null) return NotFound();

            if (option.Name       is not null) entity.Name       = option.Name;
            if (option.PriceDelta.HasValue)    entity.PriceDelta = option.PriceDelta.Value;
            if (option.Flags.HasValue)         entity.Flags      = option.Flags.Value;
            if (option.GroupId.HasValue)       entity.GroupId    = option.GroupId;

            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> DeleteOption(CatalogOptionDto option)
    {
        if (option.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Option.Id is required for delete.");
        return await DeleteOption(option.Id.Value);
    }

    public async Task<CatalogResult> DeleteOption(Guid optionId)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Options.FirstOrDefaultAsync(o => o.Id == optionId);
            if (entity is null) return NotFound();

            ctx.Options.Remove(entity);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    // ========================
    // ICatalogManager — Media
    // ========================

    public async Task<CatalogResult> AddMedia(CatalogMediaDto media)
    {
        if (string.IsNullOrWhiteSpace(media.Name))
            return Fail(CatalogResultStatus.ValidationError, "Media name is required.");
        if (string.IsNullOrWhiteSpace(media.Filepath))
            return Fail(CatalogResultStatus.ValidationError, "Media filepath is required.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = new Media
            {
                Name             = media.Name,
                Description      = media.Description,
                Filepath         = media.Filepath,
                OriginalFileName = media.OriginalFileName,
                Extension        = media.Extension        ?? string.Empty,
                MimeType         = media.MimeType         ?? string.Empty,
                MediaType        = media.MediaType        ?? Data.DataModels.V2.Catalog.MediaType.Unsupported,
                SizeBytes        = media.SizeBytes        ?? 0,
                Hash             = media.Hash             ?? string.Empty,
                GroupId          = media.GroupId,
            };
            if (media.Metadata is not null)
                foreach (var (k, v) in media.Metadata) entity.SetMetadata(k, v);

            ctx.Media.Add(entity);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> UpdateMedia(CatalogMediaDto media)
    {
        if (media.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Media.Id is required for update.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Media.FirstOrDefaultAsync(m => m.Id == media.Id.Value);
            if (entity is null) return NotFound();

            if (media.Name             is not null) entity.Name             = media.Name;
            if (media.Description      is not null) entity.Description      = media.Description;
            if (media.Filepath         is not null) entity.Filepath         = media.Filepath;
            if (media.OriginalFileName is not null) entity.OriginalFileName = media.OriginalFileName;
            if (media.Extension        is not null) entity.Extension        = media.Extension;
            if (media.MimeType         is not null) entity.MimeType         = media.MimeType;
            if (media.MediaType.HasValue)            entity.MediaType        = media.MediaType.Value;
            if (media.SizeBytes.HasValue)            entity.SizeBytes        = media.SizeBytes.Value;
            if (media.Hash             is not null) entity.Hash             = media.Hash;
            if (media.GroupId.HasValue)              entity.GroupId          = media.GroupId;
            entity.UpdatedUtc = DateTimeOffset.UtcNow;

            if (media.Metadata is not null)
                foreach (var (k, v) in media.Metadata) entity.SetMetadata(k, v);

            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> DeleteMedia(CatalogMediaDto media)
    {
        if (media.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Media.Id is required for delete.");
        return await DeleteMedia(media.Id.Value);
    }

    public async Task<CatalogResult> DeleteMedia(Guid mediaId)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Media.FirstOrDefaultAsync(m => m.Id == mediaId);
            if (entity is null) return NotFound();

            ctx.Media.Remove(entity);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    // ========================
    // ICatalogManager — Groups
    // ========================

    public async Task<CatalogResult> AddGroup(CatalogGroupDto group, CatalogGroupType groupType)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            return Fail(CatalogResultStatus.ValidationError, "Group name is required.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            switch (groupType)
            {
                case CatalogGroupType.Product:
                    ctx.ProductGroups.Add(new ProductGroup { Name = group.Name, Description = group.Description ?? string.Empty, ParentId = group.ParentId, SortPriority = group.SortPriority ?? 0 });
                    break;
                case CatalogGroupType.Option:
                    ctx.OptionGroups.Add(new OptionGroup { Name = group.Name, Description = group.Description ?? string.Empty, ParentId = group.ParentId, SortPriority = group.SortPriority ?? 0 });
                    break;
                case CatalogGroupType.Media:
                    ctx.MediaGroups.Add(new MediaGroup { Name = group.Name, Description = group.Description ?? string.Empty, ParentId = group.ParentId, SortPriority = group.SortPriority ?? 0 });
                    break;
            }
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> UpdateGroup(CatalogGroupDto group, CatalogGroupType groupType)
    {
        if (group.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Group.Id is required for update.");
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            CatalogResult result = groupType switch
            {
                CatalogGroupType.Product => await PatchGroup(ctx.ProductGroups, group),
                CatalogGroupType.Option  => await PatchGroup(ctx.OptionGroups, group),
                CatalogGroupType.Media   => await PatchGroup(ctx.MediaGroups, group),
                _                        => NotFound()
            };
            if (!result.IsSuccess) return result;
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> DeleteGroup(CatalogGroupDto group, CatalogGroupType groupType)
    {
        if (group.Id is null)
            return Fail(CatalogResultStatus.ValidationError, "Group.Id is required for delete.");
        return await DeleteGroup(group.Id.Value, groupType);
    }

    public async Task<CatalogResult> DeleteGroup(Guid groupId, CatalogGroupType groupType)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            CatalogResult result = groupType switch
            {
                CatalogGroupType.Product => await RemoveGroup(ctx.ProductGroups, ctx, groupId),
                CatalogGroupType.Option  => await RemoveGroup(ctx.OptionGroups, ctx, groupId),
                CatalogGroupType.Media   => await RemoveGroup(ctx.MediaGroups, ctx, groupId),
                _                        => NotFound()
            };
            return result;
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    // ======================================
    // ICatalogManager — Variant pricing (bulk)
    // ======================================

    public async Task<CatalogResult> UpdateVariantGroupPricing(Guid variantGroupId, decimal? price, decimal? strikePrice)
    {
        if (price is null && strikePrice is null) return Ok();
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var variants = await ctx.Variants.Where(v => v.GroupId == variantGroupId).ToListAsync();
            ApplyPricing(variants, price, strikePrice);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    public async Task<CatalogResult> UpdateVariantGroupPricing(string variantGroupName, decimal? price, decimal? strikePrice)
    {
        if (price is null && strikePrice is null) return Ok();
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var groupIds = await ctx.VariantGroups
                .Where(g => g.Name == variantGroupName)
                .Select(g => g.Id)
                .ToListAsync();
            if (groupIds.Count == 0)
                return NotFound();

            var variants = await ctx.Variants
                .Where(v => v.GroupId.HasValue && groupIds.Contains(v.GroupId.Value))
                .ToListAsync();
            ApplyPricing(variants, price, strikePrice);
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    // ========================
    // Query helpers
    // ========================

    private static IQueryable<Product> BuildProductQuery(OosDbContext ctx, ProductLocatorFlags flags)
    {
        IQueryable<Product> query = ctx.Products;
        if (flags.HasFlag(ProductLocatorFlags.IncludeVariants))
            query = query.Include(p => p.Variants);
        if (flags.HasFlag(ProductLocatorFlags.IncludeMedia))
            query = query.Include(p => p.CoverMedia).Include(p => p.Album);
        return query;
    }

    private static async Task<ICollection<CatalogOptionDto>?> LoadOptionsIfRequested(OosDbContext ctx, Guid productId, ProductLocatorFlags flags)
    {
        if (!flags.HasFlag(ProductLocatorFlags.IncludeOptions))
            return null;

        var options = await ctx.ProductOptions
            .Where(po => po.ProductId == productId)
            .Include(po => po.Option)
            .Select(po => po.Option!)
            .ToListAsync();

        return options.Select(ToOptionDto).ToList();
    }

    private async Task<List<CatalogProductDto>> MapProducts(OosDbContext ctx, List<Product> entities, ProductLocatorFlags flags)
    {
        var dtos = new List<CatalogProductDto>(entities.Count);
        foreach (var entity in entities)
        {
            var options = await LoadOptionsIfRequested(ctx, entity.Id, flags);
            dtos.Add(ToProductDto(entity, options, flags));
        }
        return dtos;
    }

    private async Task<HashSet<Guid>> CollectGroupIds(OosDbContext ctx, Guid rootId, CatalogGroupType type, ProductLocatorFlags flags)
    {
        var ids = new HashSet<Guid> { rootId };
        if (flags.HasFlag(ProductLocatorFlags.CollapseChildrenGroupMembers))
            await AddDescendantIds(ctx, rootId, type, ids);
        if (flags.HasFlag(ProductLocatorFlags.CollapseParentGroupMembers))
            await AddAncestorIds(ctx, rootId, type, ids);
        return ids;
    }

    private static async Task AddDescendantIds(OosDbContext ctx, Guid groupId, CatalogGroupType type, HashSet<Guid> ids)
    {
        var childIds = type switch
        {
            CatalogGroupType.Product => await ctx.ProductGroups.Where(g => g.ParentId == groupId).Select(g => g.Id).ToListAsync(),
            CatalogGroupType.Option  => await ctx.OptionGroups.Where(g => g.ParentId == groupId).Select(g => g.Id).ToListAsync(),
            CatalogGroupType.Media   => await ctx.MediaGroups.Where(g => g.ParentId == groupId).Select(g => g.Id).ToListAsync(),
            _                        => new List<Guid>()
        };
        foreach (var childId in childIds)
            if (ids.Add(childId))
                await AddDescendantIds(ctx, childId, type, ids);
    }

    private static async Task AddAncestorIds(OosDbContext ctx, Guid groupId, CatalogGroupType type, HashSet<Guid> ids)
    {
        var parentId = type switch
        {
            CatalogGroupType.Product => await ctx.ProductGroups.Where(g => g.Id == groupId).Select(g => g.ParentId).FirstOrDefaultAsync(),
            CatalogGroupType.Option  => await ctx.OptionGroups.Where(g => g.Id == groupId).Select(g => g.ParentId).FirstOrDefaultAsync(),
            CatalogGroupType.Media   => await ctx.MediaGroups.Where(g => g.Id == groupId).Select(g => g.ParentId).FirstOrDefaultAsync(),
            _                        => null
        };
        if (parentId.HasValue && ids.Add(parentId.Value))
            await AddAncestorIds(ctx, parentId.Value, type, ids);
    }

    private static async Task<CatalogResult> PatchGroup<TGroup>(DbSet<TGroup> set, CatalogGroupDto dto)
        where TGroup : class
    {
        // All group types share the same mutable columns; we update via the shared interface properties.
        var entity = await set.FindAsync(dto.Id!.Value);
        if (entity is null) return NotFound();

        // Update via reflection on the known shared properties — avoids duplicating this block 3×.
        var t = typeof(TGroup);
        if (dto.Name        is not null) t.GetProperty("Name")!.SetValue(entity, dto.Name);
        if (dto.Description is not null) t.GetProperty("Description")!.SetValue(entity, dto.Description);
        if (dto.ParentId.HasValue)       t.GetProperty("ParentId")!.SetValue(entity, dto.ParentId);
        if (dto.SortPriority.HasValue)   t.GetProperty("SortPriority")!.SetValue(entity, dto.SortPriority.Value);

        return Ok();
    }

    private static async Task<CatalogResult> RemoveGroup<TGroup>(DbSet<TGroup> set, OosDbContext ctx, Guid id)
        where TGroup : class
    {
        var entity = await set.FindAsync(id);
        if (entity is null) return NotFound();
        set.Remove(entity);
        await ctx.SaveChangesAsync();
        return Ok();
    }

    private static void ApplyPricing(List<Variant> variants, decimal? price, decimal? strikePrice)
    {
        foreach (var v in variants)
        {
            if (price.HasValue)       v.Price       = price.Value;
            if (strikePrice.HasValue) v.StrikePrice = strikePrice;
        }
    }

    // ========================
    // DTO mapping
    // ========================

    private static CatalogProductDto ToProductDto(Product p, ICollection<CatalogOptionDto>? options, ProductLocatorFlags flags) =>
        new(
            p.Id,
            p.Name,
            p.Description,
            p.Keywords,
            flags.HasFlag(ProductLocatorFlags.IncludeMetadata) ? p.Metadata : null,
            flags.HasFlag(ProductLocatorFlags.IncludeVariants) ? p.Variants.Select(ToVariantDto).ToList() : null,
            options,
            flags.HasFlag(ProductLocatorFlags.IncludeMedia) ? (p.CoverMedia is null ? null : ToMediaDto(p.CoverMedia)) : null,
            flags.HasFlag(ProductLocatorFlags.IncludeMedia) ? (p.Album is null ? null : ToGroupDto(p.Album)) : null,
            p.Group is null ? null : ToGroupDto(p.Group)
        );

    private static CatalogVariantDto ToVariantDto(Variant v) =>
        new(v.Id, v.GroupId, v.ProductId, v.Name, v.Price, v.StrikePrice, v.Cost, v.Sku, v.Barcode,
            v.MetadataJson is null ? null : v.Metadata);

    private static CatalogOptionDto ToOptionDto(Option o) =>
        new(o.Id, o.GroupId, o.Name, o.PriceDelta, o.Flags);

    private static CatalogMediaDto ToMediaDto(Media m) =>
        new(m.Id, m.GroupId, m.Name, m.Description, m.Filepath, m.OriginalFileName,
            m.Extension, m.MimeType, m.MediaType, m.SizeBytes, m.Hash,
            m.MetadataJson is null ? null : m.Metadata);

    private static CatalogGroupDto ToGroupDto(ProductGroup g) =>
        new(g.Id, g.ParentId, g.Name, g.Description, g.SortPriority);

    private static CatalogGroupDto ToGroupDto(OptionGroup g) =>
        new(g.Id, g.ParentId, g.Name, g.Description, g.SortPriority);

    private static CatalogGroupDto ToGroupDto(MediaGroup g) =>
        new(g.Id, g.ParentId, g.Name, g.Description, g.SortPriority);

    private static CatalogGroupDto? ToGroupDtoOrNull(ProductGroup? g) => g is null ? null : ToGroupDto(g);
    private static CatalogGroupDto? ToGroupDtoOrNull(OptionGroup? g)  => g is null ? null : ToGroupDto(g);
    private static CatalogGroupDto? ToGroupDtoOrNull(MediaGroup? g)   => g is null ? null : ToGroupDto(g);

    // ========================
    // Result factories
    // ========================

    private static CatalogResult Ok()         => new();
    private static CatalogResult NotFound()   => new() { Status = CatalogResultStatus.NotFound };
    private static CatalogResult StorageError(Exception ex) => new() { Status = CatalogResultStatus.StorageError, Message = ex.Message };
    private static CatalogResult Fail(CatalogResultStatus status, string message) => new() { Status = status, Message = message };

    private static CatalogProductQueryResult OkProduct(CatalogProductDto dto, ProductLocatorFlags flags = ProductLocatorFlags.None) =>
        new() { Results = [dto], WithFlags = flags };

    private static CatalogProductQueryResult OkProducts(IReadOnlyList<CatalogProductDto> dtos, ProductLocatorFlags flags) =>
        new() { Results = dtos, WithFlags = flags };

    private static CatalogProductQueryResult NotFoundProduct(ProductLocatorFlags flags = ProductLocatorFlags.None) =>
        new() { Status = CatalogResultStatus.NotFound, WithFlags = flags };

    private static CatalogProductQueryResult ErrorProduct(Exception ex, ProductLocatorFlags flags = ProductLocatorFlags.None) =>
        new() { Status = CatalogResultStatus.StorageError, Message = ex.Message, WithFlags = flags };
}