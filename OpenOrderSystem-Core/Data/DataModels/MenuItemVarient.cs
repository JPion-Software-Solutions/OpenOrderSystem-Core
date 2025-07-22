using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class MenuItemVarient
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Varient descriptor (ex: Large/Medium or 12pc/16pc)
        /// </summary>
        [MaxLength(12, ErrorMessage = "Please keep item varient descriptors shorter than 12 characters.")]
        [Required]
        public string Descriptor { get; set; } = string.Empty;

        [Required]
        public int Index { get; set; } = -1;

        /// <summary>
        /// Gives a value to sort item by. Higher number, further up the page. Negative number, item is hidden
        /// </summary>
        public int Priority { get; set; } = 0;

        [NotMapped]
        public virtual bool IsArchived => Priority <= 0;

        /// <summary>
        /// Price of the varient
        /// </summary>
        [Required]
        public float Price { get; set; }

        /// <summary>
        /// Upc used to construct bar code
        /// </summary>
        [Required]
        public string Upc { get; set; } = string.Empty;

        /// <summary>
        /// Primary key of the menu item this varient belongs to.
        /// </summary>
        [Required]
        public int MenuItemId { get; set; }

        /// <summary>
        /// Navigation property for the varient's corrisponding menu item
        /// </summary>
        public MenuItem? MenuItem { get; set; }

        public List<BaseDiscountCode>? DiscountCodes { get; set; }
    }
}
