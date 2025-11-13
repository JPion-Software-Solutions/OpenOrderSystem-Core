using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class Customer
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Full name of the customer
        /// </summary>
        [Required(ErrorMessage = "Please enter a name for your order.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Phone number of the customer
        /// </summary>
        [Required(ErrorMessage = "Please enter a phone number.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Email of the customer
        /// </summary>
        [Required(ErrorMessage = "Please enter an email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp the customer was created.
        /// </summary>
        public DateTime CustomerCreated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// True if customer requests updates by email
        /// </summary>
        public bool EmailUpdates { get; set; } = false;

        /// <summary>
        /// True (default) if customer request SMS order updates
        /// </summary>
        public bool SMSUpdates { get; set; } = true;
    }
}
