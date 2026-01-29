using System;
using System.Security.Policy;
using System.Text.Json.Serialization;
using Microsoft.Identity.Client;

namespace OpenOrderSystem.Core.DevelopmentTools;

public sealed class DevPackManifest
{
    [JsonPropertyName("packs")]
    public List<PackEntry> Packs { get; set; } = new();

    [JsonPropertyName("default")]
    public string DefaultPackRef { get; set; } = "";
}

public sealed class PackEntry
{
    [JsonPropertyName("packname")]
    public string PackName { get; set; } = "";

    [JsonPropertyName("latest")]
    public string Latest { get; set; } = "";

    [JsonPropertyName("versions")]
    public Dictionary<string, PackVersion> Versions { get; set; } = new();
}

public sealed class PackVersion
{
    [JsonPropertyName("oos_version")]
    public string OosVersion { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = "";

    // JSON field is "hash", but we expose it as a clear intent name in C#.
    // Your sample looks like a SHA-256 hex string (64 hex chars).
    [JsonPropertyName("hash")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("group")]
    public string Group { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public sealed class DevPackInfo
{
    public record RestaurantInfo (string DisplayName, string Tagline, string Timezone, Contactnfo Contact, AddressInfo Address);
    public record Contactnfo (string Phone, string Website);
    public record AddressInfo (string Line1, string Line2, string City, string State, string PostalCode, string Country);
    public record HoursInfo (string Day, string Open, string Close, bool IsClosed);
    public record WeeklyHoursInfo(List<HoursInfo> Weekly);

    public RestaurantInfo Restaurant { get; set; } = new("", "", "", new("", ""), new ("", "", "", "", "", ""));

    public WeeklyHoursInfo Hours { get; set; } = new (new());
}

public sealed class DevPackMenu
{
    public record IngredientCategory(
        string Key = "ERROR", 
        string Name = "", 
        int Priority = 0, 
        string Type = ""
    );
    
    public record Ingredient(
        string Key = "ERROR",
        string CategoryKey = "",
        string Name = "",
        float Price = 0.0f
    );

    public record ProductCategory(
        string Key = "ERROR",
        string Name = "",
        int Priority = 0,
        string Description = "",
        List<string>? AllowedIngredientKeys = null
    );

    public record Varient(
        string Descriptor = "ERROR",
        int Index = 0,
        int Priority = 0,
        float Price = 0.0f,
        string Upc = ""
    );

    public record MenuItem(
        string Key = "ERROR",
        string Name = "",
        int Priority = 0,
        string Description = "",
        string ImageUrl = "",
        string ProductCategoryKey = "",
        List<string>? DefaultIngredientKeys = null,
        List<Varient>? Varients = null
    );

    public List<IngredientCategory> IngredientCategories { get; set; } = new();

    public List<Ingredient> Ingredients { get; set; } = new();

    public List<ProductCategory> ProductCategories { get; set; } = new();

    public List<MenuItem> MenuItems { get; set; } = new();
}

public sealed class DevPackPrinterTemplates
{
    public class PackBuildStep
    {
        public string Instruction { get; set; }  = "ERROR";
        public string Data { get; set; } = "";
    }

    public record Template(
        string Key = "ERROR", 
        string Name = "", 
        bool DefaultOrderTemplate = false, 
        bool DefaultEndOfDayTemplate = false, 
        List<PackBuildStep>? Steps = null
    );

    public List<Template> Templates { get; set; } = new List<Template>();
}