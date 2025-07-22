namespace OpenOrderSystem.Core.Areas.Configuration.Models
{
    public class ScheduleDay
    {
        /// <summary>
        /// Day the schedule is applied to.
        /// </summary>
        public virtual DayOfWeek Day { get; set; } = DayOfWeek.Saturday;

        /// <summary>
        /// Start of order schduling
        /// </summary>
        public TimeOnly Open { get; set; }

        /// <summary>
        /// End of order scheduling
        /// </summary>
        public TimeOnly Close { get; set; }

        public override string ToString()
        {
            return $"{Day}: {Open} to {Close}";
        }
    }
}
