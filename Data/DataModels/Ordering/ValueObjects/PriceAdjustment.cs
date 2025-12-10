namespace OpenOrderSystem.Core.Data.DataModels.Ordering.ValueObjects
{
    public class PriceAdjustment
    {
        private string _source = string.Empty;
        public static HashSet<string> ValidSources { get; private set; } = new HashSet<string>
        {
            "OOSCore.ManualAdjust",
            "OOSCore.Promotion",
            "OOSCore.StaffOverride",
            "OOSCore.Tax"
        };
        
        public static void RegisterSource(string source) => ValidSources.Add(source);

        public static bool IsValidSource(string source) => ValidSources.Contains(source, StringComparer.OrdinalIgnoreCase);
        
        public float Amount { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string Source
        {
            get => _source;
            set => _source = IsValidSource(value) 
                ? value 
                : throw new InvalidOperationException($"The source '{value}' is not a known valid PriceAdjustment source");
        }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool MatchesSource(string source) => Source.ToLowerInvariant() == source.ToLowerInvariant();
    }
}
