namespace OpenOrderSystem.Core.Areas.Staff.Models.Widgets
{
    public interface IWidget
    {
        /// <summary>
        /// Title of the widget
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Url used to fetch widget
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Determines how much horrizontal space a widget occupies
        /// </summary>
        WidgetSize Size { get; }

        /// <summary>
        /// Time between widget refresh requests.
        /// </summary>
        public int RefreshTime { get; protected set; }

        /// <summary>
        /// Data contained within the widget
        /// </summary>
        public Dictionary<string, string> Data { get; protected set; }
    }

    public enum WidgetSize
    {
        /// <summary>
        /// Widget will occupy 1/4 of the screen width (1/2 on med devices)
        /// </summary>
        Small,

        /// <summary>
        /// Widget will occupy 1/2 the screen width (100% on med devices)
        /// </summary>
        Medium,

        /// <summary>
        /// Widget occupies full screen witdth on all devices.
        /// </summary>
        Large
    }
}
