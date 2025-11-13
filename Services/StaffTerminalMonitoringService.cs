namespace OpenOrderSystem.Core.Services
{
    public class StaffTerminalMonitoringService
    {
        private static readonly string SYSTEM_TRIGGER_IDENTIFIER = Guid.NewGuid().ToString();
        private static readonly string NEW_ORDER_ALERT = SYSTEM_TRIGGER_IDENTIFIER + "_" + Guid.NewGuid().ToString();
        private static readonly string TIME_LOW_ALERT = SYSTEM_TRIGGER_IDENTIFIER + "_" + Guid.NewGuid().ToString();
        private DateTime _lowTimeAlertCooldown = DateTime.UtcNow;
        private ConfigurationService _config;

        private readonly Dictionary<string, bool> _actionTriggers = new Dictionary<string, bool>
        {
            { NEW_ORDER_ALERT, false },     //new order in system
            { TIME_LOW_ALERT, false }       //order timer about to run out.
        };

        /// <summary>
        /// Initializes the terminal monitoring service
        /// </summary>
        /// <param name="terminalTimout">Time in seconds before the terminal will be reported as offline</param>
        public StaffTerminalMonitoringService(ConfigurationService config, double terminalTimout = 60)
        {
            TerminalTimeout = terminalTimout;
            _config = config;
        }

        /// <summary>
        /// Time in seconds before the terminal will be reported as offline
        /// </summary>
        public double TerminalTimeout { get; set; } = 60;

        /// <summary>
        /// Time when the terminal was last recognized as online.
        /// </summary>
        public DateTime LastTerminalCheckin { get; private set; }

        /// <summary>
        /// Checks if the terminal is currently active.
        /// </summary>
        public bool TerminalActive
        {
            get
            {
                return DateTime.UtcNow < LastTerminalCheckin.AddSeconds(TerminalTimeout);
            }
        }

        /// <summary>
        /// Checks if a new order alert has been triggered
        /// </summary>
        public bool NewOrderAlert
        {
            get
            {
                if (_actionTriggers[NEW_ORDER_ALERT])
                {
                    _actionTriggers[NEW_ORDER_ALERT] = false;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Triggers a new order alert
        /// </summary>
        public void TriggerNewOrderAlert() => _actionTriggers[NEW_ORDER_ALERT] = true;

        /// <summary>
        /// Checks if a low time alert has been triggered
        /// </summary>
        public bool OrderTimerAlert
        {
            get
            {
                if (_actionTriggers[TIME_LOW_ALERT])
                {
                    _actionTriggers[TIME_LOW_ALERT] = false;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Triggers a low time alert
        /// </summary>
        public void TriggerOrderTimerAlert()
        {
            if (_lowTimeAlertCooldown <= DateTime.UtcNow)
            {
                _actionTriggers[TIME_LOW_ALERT] = true;
                _lowTimeAlertCooldown = DateTime.UtcNow.AddMinutes(1.75);
            }
        }

        /// <summary>
        /// Checks if a generic trigger is active then returns true and deactivates if it is.
        /// </summary>
        /// <param name="trigger">name of trigger to check</param>
        /// <returns>true if trigger is active</returns>
        public bool CheckGenericTrigger(string trigger)
        {
            if (_actionTriggers.ContainsKey(trigger))
            {
                _actionTriggers.Remove(trigger);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Triggers a generic terminal notification
        /// </summary>
        /// <param name="trigger">name of trigger to activate</param>
        public void TriggerGeneric(string trigger) => _actionTriggers[trigger] = true;

        /// <summary>
        /// Retrieves a list of all generic triggers (excludes system triggers)
        /// </summary>
        public Dictionary<string, bool> GenericTriggers
        {
            get
            {
                var triggers = new Dictionary<string, bool>();

                foreach (var trigger in _actionTriggers.Keys)
                {
                    if (trigger.Contains(SYSTEM_TRIGGER_IDENTIFIER)) continue;
                    triggers[trigger] = _actionTriggers[trigger];
                }

                return triggers;
            }
        }

        /// <summary>
        /// Register that the staff ordering terminal has checked in recently.
        /// </summary>
        public void RegisterCheckin() => LastTerminalCheckin = DateTime.UtcNow;

        public void CloseTerminal() => LastTerminalCheckin = DateTime.UtcNow.AddDays(-100);
    }
}
