using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class ConfirmationCode
    {
        [NotMapped]
        public static int CodesIssued { get; set; }

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
        public DateTime Expiration { get; set; }

        public string Code { get; set; }

        [NotMapped]
        public bool IsValid
        {
            get => Expiration > DateTime.UtcNow;
        }

        public string GenerateCode()
        {
            var guid = Id.Split('-');
            var code = $"{CodesIssued.ToString("D4")}-";

            foreach (var item in guid)
            {
                var rand = new Random();
                var i = rand.Next(item.Length);
                code += item[i];
            }

            CodesIssued++;
            Code = code;
            Expiration = DateTime.UtcNow.AddMinutes(60);
            return code;
        }
    }
}
