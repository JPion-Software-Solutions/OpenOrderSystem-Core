namespace OpenOrderSystem.Core.Areas.Configuration.Models
{
    public class ScheduleException : ScheduleDay
    {
        /// <summary>
        /// Date this exception applies to
        /// </summary>
        public DateOnly Date { get; set; }

        /// <summary>
        /// Returns the day of the week associated with the ScheduleException
        /// </summary>
        public override DayOfWeek Day
        {
            get => Date.DayOfWeek;
        }

        /// <summary>
        /// Determines if the exception should be auto purged
        /// </summary>
        public bool Purge
        {
            get => DateTime.Today.Date > Date.ToDateTime(new TimeOnly());
        }
    }
}
