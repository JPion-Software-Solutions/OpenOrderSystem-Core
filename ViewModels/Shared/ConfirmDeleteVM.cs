namespace OpenOrderSystem.Core.ViewModels.Shared
{
    public class ConfirmDeleteVM
    {
        public string Action { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
