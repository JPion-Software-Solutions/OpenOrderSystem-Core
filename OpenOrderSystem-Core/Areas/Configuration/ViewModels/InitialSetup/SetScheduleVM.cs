namespace OpenOrderSystem.Core.Areas.Configuration.ViewModels.InitialSetup
{
    public class SetScheduleVM
    {
        public readonly string[] DayLabels =
        {
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday"
        };
        public TimeOnly[] OpenTimes { get; set; } = new TimeOnly[7];
        public TimeOnly[] CloseTimes { get; set; } = new TimeOnly[7];
    }
}
