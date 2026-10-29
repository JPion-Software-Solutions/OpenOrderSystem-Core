using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            var snapshot = await LoadSnapshotIfRequested(ctx, entity.Id, flags);
            return OkProduct(ToProductDto(entity, options, flags, snapshot), flags);
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
            var snapshot = await LoadSnapshotIfRequested(ctx, entity.Id, flags);
            return OkProduct(ToProductDto(entity, options, flags, snapshot), flags);
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
            var snapshot = await LoadSnapshotIfRequested(ctx, entity.Id, flags);
            return OkProduct(ToProductDto(entity, options, flags, snapshot), flags);
        }
        catch (Exception ex) { return ErrorProduct(ex, flags); }
    }

    // ===========================
    // IReadOnlyCatalog — Variants
    // ===========================

    public async Task<CatalogVariantQueryResult> FindVariant(Guid id)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Variants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && !v.Product!.IsArchived);
            return entity is null
                ? new CatalogVariantQueryResult { Status = CatalogResultStatus.NotFound }
                : new CatalogVariantQueryResult { Results = [ToVariantDto(entity)] };
        }
        catch (Exception ex) { return new CatalogVariantQueryResult { Status = CatalogResultStatus.StorageError, Message = ex.Message }; }
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

            var (error, options) = await ValidateChildrenAsync(ctx, entity.Id, new HashSet<Guid>(), product);
            if (error is not null)
                return new CatalogProductQueryResult { Status = error.Status, Message = error.Message };

            ctx.Products.Add(entity);
            if (product.Variants is not null)
                ApplyVariants(ctx, entity, product.Variants);
            var links = product.Options is null ? [] : ApplyOptionLinks(ctx, entity.Id, [], product.Options, options);

            var linkDtos = links.Select(ToProductOptionDto).ToList();
            await StageSnapshotAsync(ctx, entity, linkDtos);
            await ctx.SaveChangesAsync();
            return OkProduct(ToProductDto(entity, linkDtos, ProductLocatorFlags.IncludeVariants), ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeOptions);
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
            // Archived products remain editable so they can be corrected before being restored.
            var entity = await BuildProductQuery(ctx, ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeMedia | ProductLocatorFlags.IncludeArchived)
                .FirstOrDefaultAsync(p => p.Id == product.Id.Value);
            if (entity is null)
                return NotFoundProduct();

            var (error, options) = await ValidateChildrenAsync(ctx, entity.Id, entity.Variants.Select(v => v.Id).ToHashSet(), product);
            if (error is not null)
                return new CatalogProductQueryResult { Status = error.Status, Message = error.Message };

            if (product.Name        is not null) entity.Name        = product.Name;
            if (product.Description is not null) entity.Description = product.Description;
            if (product.Keywords    is not null) entity.Keywords    = product.Keywords;
            if (product.CoverMedia?.Id      is not null) entity.CoverMediaId = product.CoverMedia.Id;
            if (product.MediaAlbum?.Id      is not null) entity.AlbumId      = product.MediaAlbum.Id;
            if (product.CatalogProductGroup?.Id is not null) entity.GroupId  = product.CatalogProductGroup.Id;
            if (product.Metadata is not null)
                foreach (var (k, v) in product.Metadata) entity.SetMetadata(k, v);

            if (product.Variants is not null)
                ApplyVariants(ctx, entity, product.Variants);

            var existingLinks = await ctx.ProductOptions
                .Include(po => po.Option)
                .Where(po => po.ProductId == entity.Id)
                .ToListAsync();
            var links = product.Options is null
                ? existingLinks
                : ApplyOptionLinks(ctx, entity.Id, existingLinks, product.Options, options);

            var linkDtos = links.Select(ToProductOptionDto).ToList();
            await StageSnapshotAsync(ctx, entity, linkDtos);
            await ctx.SaveChangesAsync();
            return OkProduct(ToProductDto(entity, linkDtos, ProductLocatorFlags.IncludeVariants), ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeOptions);
        }
        catch (Exception ex) { return ErrorProduct(ex); }
    }

    public async Task<CatalogProductQueryResult> ArchiveProduct(CatalogProductDto product)
    {
        if (product.Id is null)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product.Id is required for archive." };
        return await ArchiveProduct(product.Id.Value);
    }

    public Task<CatalogProductQueryResult> ArchiveProduct(Guid productId) => SetArchived(productId, true);

    public async Task<CatalogProductQueryResult> RestoreProduct(CatalogProductDto product)
    {
        if (product.Id is null)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = "Product.Id is required for restore." };
        return await RestoreProduct(product.Id.Value);
    }

    public Task<CatalogProductQueryResult> RestoreProduct(Guid productId) => SetArchived(productId, false);

    public async Task<CatalogProductQueryResult> UpdateProductFlags(Guid productId, ProductFlags set = ProductFlags.None, ProductFlags clear = ProductFlags.None)
    {
        if ((set & clear) != ProductFlags.None)
            return new CatalogProductQueryResult { Status = CatalogResultStatus.ValidationError, Message = $"Flags cannot be both set and cleared: {set & clear}." };
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (entity is null)
                return NotFoundProduct();

            var flags = (entity.Flags | set) & ~clear;
            if (flags != entity.Flags)
            {
                entity.Flags = flags;
                await ctx.SaveChangesAsync();
            }
            return OkProduct(ToProductDto(entity, null, ProductLocatorFlags.None), ProductLocatorFlags.IncludeArchived);
        }
        catch (Exception ex) { return ErrorProduct(ex); }
    }

    private async Task<CatalogProductQueryResult> SetArchived(Guid productId, bool archived)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync();
            var entity = await ctx.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (entity is null)
                return NotFoundProduct();

            if (entity.IsArchived != archived)
            {
                entity.IsArchived = archived;
                await ctx.SaveChangesAsync();
            }
            return OkProduct(ToProductDto(entity, null, ProductLocatorFlags.None), ProductLocatorFlags.IncludeArchived);
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
            await SnapshotAffectedProductsAsync(ctx, variants.Select(v => v.ProductId).Distinct());
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
            await SnapshotAffectedProductsAsync(ctx, variants.Select(v => v.ProductId).Distinct());
            await ctx.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex) { return StorageError(ex); }
    }

    // ========================
    // Product child helpers
    // ========================

    /// <summary>
    /// Validates the variant and option-link sets on <paramref name="dto"/> before anything is mutated.
    /// Returns the referenced options (tracked) so new links can be wired without a second lookup.
    /// </summary>
    private static async Task<(CatalogResult? Error, Dictionary<Guid, Option> Options)> ValidateChildrenAsync(
        OosDbContext ctx, Guid productId, IReadOnlySet<Guid> existingVariantIds, CatalogProductDto dto)
    {
        var options = new Dictionary<Guid, Option>();

        if (dto.Variants is not null)
        {
            foreach (var v in dto.Variants)
            {
                if (string.IsNullOrWhiteSpace(v.Name))
                    return (Fail(CatalogResultStatus.ValidationError, "Variant name is required."), options);
                if (v.Price is null or < 0m)
                    return (Fail(CatalogResultStatus.ValidationError, $"Variant '{v.Name}' requires a price of 0 or more."), options);
                if (v.ProductId.HasValue && v.ProductId != productId)
                    return (Fail(CatalogResultStatus.ValidationError, $"Variant '{v.Name}' cannot be moved to another product."), options);
                if (v.Id.HasValue && !existingVariantIds.Contains(v.Id.Value))
                    return (Fail(CatalogResultStatus.ValidationError, $"Variant '{v.Id}' does not belong to this product."), options);
            }

            var ids = dto.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToList();
            if (ids.Count != ids.Distinct().Count())
                return (Fail(CatalogResultStatus.ValidationError, "Duplicate variant Id in request."), options);

            var groupIds = dto.Variants.Where(v => v.GroupId.HasValue).Select(v => v.GroupId!.Value).Distinct().ToList();
            if (groupIds.Count > 0 && await ctx.VariantGroups.CountAsync(g => groupIds.Contains(g.Id)) != groupIds.Count)
                return (Fail(CatalogResultStatus.ValidationError, "One or more variant groups do not exist."), options);

            var skus = dto.Variants.Where(v => v.Sku is not null).Select(v => v.Sku!).ToList();
            if (skus.Count != skus.Distinct().Count())
                return (Fail(CatalogResultStatus.Conflict, "Duplicate SKU in request."), options);
            if (skus.Count > 0)
            {
                var taken = await ctx.Variants
                    .Where(x => x.ProductId != productId && x.Sku != null && skus.Contains(x.Sku))
                    .Select(x => x.Sku)
                    .FirstOrDefaultAsync();
                if (taken is not null)
                    return (Fail(CatalogResultStatus.Conflict, $"SKU '{taken}' is already used by another product."), options);
            }
        }

        if (dto.Options is not null)
        {
            if (dto.Options.Any(o => o.OptionId is null))
                return (Fail(CatalogResultStatus.ValidationError, "Option link requires an OptionId."), options);
            if (dto.Options.Any(o => o.ProductId.HasValue && o.ProductId != productId))
                return (Fail(CatalogResultStatus.ValidationError, "Option link ProductId does not match the product."), options);

            var optionIds = dto.Options.Select(o => o.OptionId!.Value).ToList();
            if (optionIds.Count != optionIds.Distinct().Count())
                return (Fail(CatalogResultStatus.ValidationError, "Duplicate OptionId in request."), options);

            options = await ctx.Options.Where(o => optionIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            if (options.Count != optionIds.Count)
                return (Fail(CatalogResultStatus.ValidationError, "One or more options do not exist."), options);
        }

        return (null, options);
    }

    /// <summary>
    /// Syncs <paramref name="entity"/>'s variants to <paramref name="dtos"/> (full set, full state per entry).
    /// Must be called after validation.
    /// </summary>
    private static void ApplyVariants(OosDbContext ctx, Product entity, ICollection<CatalogVariantDto> dtos)
    {
        var keep = dtos.Where(d => d.Id.HasValue).Select(d => d.Id!.Value).ToHashSet();
        foreach (var removed in entity.Variants.Where(v => !keep.Contains(v.Id)).ToList())
        {
            entity.Variants.Remove(removed);
            ctx.Variants.Remove(removed);
        }

        var byId = entity.Variants.ToDictionary(v => v.Id);
        foreach (var dto in dtos)
        {
            Variant target;
            if (dto.Id.HasValue)
            {
                target = byId[dto.Id.Value];
            }
            else
            {
                // Explicit Add: a client-generated Guid key would otherwise be discovered as Unchanged.
                target = new Variant { ProductId = entity.Id };
                ctx.Variants.Add(target);
                entity.Variants.Add(target);
            }

            target.Name         = dto.Name!;
            target.Price        = dto.Price!.Value;
            target.StrikePrice  = dto.StrikePrice;
            target.Cost         = dto.Cost;
            target.Sku          = dto.Sku;
            target.Barcode      = dto.Barcode;
            target.GroupId      = dto.GroupId;
            target.MetadataJson = dto.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(dto.Metadata) : null;
        }
    }

    /// <summary>
    /// Syncs the product's option links to <paramref name="dtos"/> (full set, full state per entry) and
    /// returns the resulting link set. Must be called after validation.
    /// </summary>
    private static List<ProductOption> ApplyOptionLinks(OosDbContext ctx, Guid productId, List<ProductOption> existing,
        ICollection<CatalogProductOptionDto> dtos, Dictionary<Guid, Option> options)
    {
        var keep = dtos.Select(d => d.OptionId!.Value).ToHashSet();
        ctx.ProductOptions.RemoveRange(existing.Where(po => !keep.Contains(po.OptionId)));

        var byOption = existing.Where(po => keep.Contains(po.OptionId)).ToDictionary(po => po.OptionId);
        var result = new List<ProductOption>(dtos.Count);
        foreach (var dto in dtos)
        {
            var optionId = dto.OptionId!.Value;
            if (!byOption.TryGetValue(optionId, out var link))
            {
                link = new ProductOption { ProductId = productId, OptionId = optionId, Option = options[optionId] };
                ctx.ProductOptions.Add(link);
            }

            link.DefaultState       = dto.DefaultState ?? ProductOptionDefaultState.Optional;
            link.PriceDeltaOverride = dto.PriceDeltaOverride;
            result.Add(link);
        }
        return result;
    }

    // ========================
    // Snapshot helpers
    // ========================

    private async Task StageSnapshotAsync(OosDbContext ctx, Product entity, ICollection<CatalogProductOptionDto>? options)
    {
        var flags = ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeMedia | ProductLocatorFlags.IncludeMetadata;
        // Archive state and flags are operational state, not product content, so they never produce a new snapshot.
        var dto = ToProductDto(entity, options, flags) with { IsArchived = null, Flags = null, FlagStates = null };
        var json = JsonSerializer.Serialize(dto);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        var current = await ctx.ProductSnapshots
            .AsNoTracking()
            .Where(s => s.ProductId == entity.Id)
            .OrderByDescending(s => s.CreatedUtc)
            .Select(s => new { s.Id, s.SnapshotHash })
            .FirstOrDefaultAsync();

        if (current?.SnapshotHash == hash)
            return;

        ctx.ProductSnapshots.Add(new ProductSnapshot
        {
            ProductId        = entity.Id,
            PreviousSnapshotId = current?.Id,
            SnapshotJson     = json,
            SnapshotHash     = hash
        });
    }

    private async Task SnapshotAffectedProductsAsync(OosDbContext ctx, IEnumerable<Guid> productIds)
    {
        foreach (var productId in productIds)
        {
            var product = await BuildProductQuery(ctx, ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeMedia | ProductLocatorFlags.IncludeArchived)
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (product is null) continue;
            var options = await LoadOptionsIfRequested(ctx, productId, ProductLocatorFlags.IncludeOptions);
            await StageSnapshotAsync(ctx, product, options);
        }
    }

    // ========================
    // Query helpers
    // ========================

    private static IQueryable<Product> BuildProductQuery(OosDbContext ctx, ProductLocatorFlags flags)
    {
        IQueryable<Product> query = ctx.Products;
        if (!flags.HasFlag(ProductLocatorFlags.IncludeArchived))
            query = query.Where(p => !p.IsArchived);
        if (flags.HasFlag(ProductLocatorFlags.IncludeVariants))
            query = query.Include(p => p.Variants);
        if (flags.HasFlag(ProductLocatorFlags.IncludeMedia))
            query = query.Include(p => p.CoverMedia).Include(p => p.Album);
        if  (flags.HasFlag(ProductLocatorFlags.IncludeSnapshot))
            query = query.Include(p => p.Snapshots);
        return query;
    }

    private static async Task<CatalogProductSnapshotDto?> LoadSnapshotIfRequested(OosDbContext ctx, Guid productId, ProductLocatorFlags flags)
    {
        if (!flags.HasFlag(ProductLocatorFlags.IncludeSnapshot))
            return null;

        var snapshot = await ctx.ProductSnapshots
            .AsNoTracking()
            .Where(s => s.ProductId == productId)
            .OrderByDescending(s => s.CreatedUtc)
            .FirstOrDefaultAsync();

        return snapshot is null ? null : new CatalogProductSnapshotDto(
            snapshot.Id,
            snapshot.ProductId,
            snapshot.PreviousSnapshotId,
            snapshot.SchemaVersion,
            snapshot.SnapshotJson,
            snapshot.SnapshotHash);
    }

    private static async Task<ICollection<CatalogProductOptionDto>?> LoadOptionsIfRequested(OosDbContext ctx, Guid productId, ProductLocatorFlags flags)
    {
        if (!flags.HasFlag(ProductLocatorFlags.IncludeOptions))
            return null;

        var links = await ctx.ProductOptions
            .Where(po => po.ProductId == productId)
            .Include(po => po.Option)
            .ToListAsync();

        return links.Select(ToProductOptionDto).ToList();
    }

    private async Task<List<CatalogProductDto>> MapProducts(OosDbContext ctx, List<Product> entities, ProductLocatorFlags flags)
    {
        var dtos = new List<CatalogProductDto>(entities.Count);
        foreach (var entity in entities)
        {
            var options = await LoadOptionsIfRequested(ctx, entity.Id, flags);
            var snapshot = await LoadSnapshotIfRequested(ctx, entity.Id, flags);
            dtos.Add(ToProductDto(entity, options, flags, snapshot));
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

    private static CatalogProductDto ToProductDto(Product p, ICollection<CatalogProductOptionDto>? options, ProductLocatorFlags flags, CatalogProductSnapshotDto? snapshot = null) =>
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
            p.Group is null ? null : ToGroupDto(p.Group),
            snapshot,
            p.IsArchived,
            p.Flags,
            ToFlagStates(p.Flags)
        );

    private static readonly ProductFlags[] NamedProductFlags =
        Enum.GetValues<ProductFlags>().Where(f => f != ProductFlags.None).ToArray();

    /// <summary>Every named <see cref="ProductFlags"/> value, keyed by name, so frontends never decode the bitmask.</summary>
    private static IReadOnlyDictionary<string, bool> ToFlagStates(ProductFlags flags) =>
        NamedProductFlags.ToDictionary(f => f.ToString(), f => flags.HasFlag(f));

    private static CatalogVariantDto ToVariantDto(Variant v) =>
        new(v.Id, v.GroupId, v.ProductId, v.Name, v.Price, v.StrikePrice, v.Cost, v.Sku, v.Barcode,
            v.MetadataJson is null ? null : v.Metadata);

    private static CatalogOptionDto ToOptionDto(Option o) =>
        new(o.Id, o.GroupId, o.Name, o.PriceDelta, o.Flags);

    private static CatalogProductOptionDto ToProductOptionDto(ProductOption po) =>
        new(po.ProductId, po.OptionId, po.DefaultState, po.PriceDeltaOverride,
            po.Option is null ? null : ToOptionDto(po.Option),
            po.Option is null && po.PriceDeltaOverride is null && po.DefaultState != ProductOptionDefaultState.Included
                ? null
                : po.EffectivePriceDelta);

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