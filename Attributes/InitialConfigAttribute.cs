namespace OpenOrderSystem.Core.Attributes
{
    /// <summary>
    /// Invoke the initial configuration checking middleware to redirect the user depending on 
    /// whether or not the initial configuration has been performed. By default a redirect occurs
    /// when the configuration has not been completed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class InitialConfigAttribute : Attribute
    {
        /// <summary>
        /// Create an initial config check/redirect.
        /// </summary>
        /// <param name="redirectOnFail">URL to redirect the user to.</param>
        /// <param name="invertCondition">OPTIONAL: invert the redirect condition to redirect 
        /// when the site IS configured.</param>
        public InitialConfigAttribute(string redirectOnFail, bool invertCondition = false)
        {
            RedirectOnFail = redirectOnFail;
            InvertCondition = invertCondition;
        }

        public string RedirectOnFail { get; set; }
        public bool InvertCondition { get; set; }
    }
}
