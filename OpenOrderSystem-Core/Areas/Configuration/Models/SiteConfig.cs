using Newtonsoft.Json;
using System.Data;

namespace OpenOrderSystem.Core.Areas.Configuration.Models
{
    /// <summary>
    /// Stores the current configuration data for the site.
    /// </summary>
    public class SiteConfig
    {
        /// <summary>
        /// defines the how long (in hours) customer data is retained in the database before it is purged from the database.
        /// </summary>
        public int CustomerDataRetentionPolicy { get; set; } = 24;

        /// <summary>
        /// Hash value of the recovery key used to recover the system in the event of total admin account loss.
        /// </summary>
        public string RecoveryKeyHash { get; set; } = string.Empty;

        /// <summary>
        /// defines the address used to connect to a reciept printer.
        /// </summary>
        public string LocalPrinterAddress { get; set; } = string.Empty;

        /// <summary>
        /// Hours the system is active
        /// </summary>
        public ScheduleDay[] Schedule { get; set; } = new ScheduleDay[]
        {
            new ScheduleDay
            {
                Day = DayOfWeek.Sunday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Monday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Tuesday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Wednesday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Thursday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Friday
            },
            new ScheduleDay
            {
                Day = DayOfWeek.Saturday
            }
        };

        public List<ScheduleException> ScheduleExceptions { get; set; } = new List<ScheduleException>();

        /// <summary>
        /// Used to temporarily override the schedule if necessary
        /// </summary>
        public static bool OverrideSchedule { get; set; } = false;

        /// <summary>
        /// Checks for exceptions and gets the schedule for today.
        /// </summary>
        [JsonIgnore]
        public ScheduleDay Today
        {
            get
            {
                var today = Schedule.FirstOrDefault(d => d.Day == DateTime.Now.DayOfWeek);

                if (today == null)
                    today = Schedule[0];

                if (ScheduleExceptions.Any())
                {
                    var exceptionToday = ScheduleExceptions.FirstOrDefault(e => e.Date.ToDateTime(new TimeOnly()) == DateTime.Today.Date);

                    if (exceptionToday != null)
                        today = exceptionToday;
                }

                return today;
            }
        }

        /// <summary>
        /// Marks if the system is currently taking orders. Checks for both schedule override and exceptions before 
        /// returning the current status
        /// </summary>
        [JsonIgnore]
        public bool AcceptingOrders
        {
            get
            {
                if (OverrideSchedule)
                    return false;

                var utcTime = DateTime.UtcNow;
                TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var localTimeZone);
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, localTimeZone ?? TimeZoneInfo.Local).TimeOfDay;


                return currentTime >= Today.Open.ToTimeSpan() && currentTime <= Today.Close.ToTimeSpan();
            }
        }
    }

}
