using System.Drawing;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class Printer
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Pin { get; set; } = string.Empty;

        public string Client { get; set; } = string.Empty;
        public bool DefaultOrderPrinter { get; set; } = false;

        public bool DefaultEndOfDayPrinter { get; set; } = false;

        public void SetPin(string pin)
        {
            string saltShaker = "";

            using (var random = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[16];
                random.GetBytes(randomBytes, 0, 16);
                saltShaker = Convert.ToHexString(randomBytes);
            }

            Pin = HashString(pin, saltShaker);
        }

        public bool ValidatePin(string pin)
        {
            var splitPin = Pin.Split(':');
            if (splitPin.Length != 2) return false;

            var salt = splitPin[1];

            return Pin == HashString(pin, salt);
        }

        public void SetClient(string clientId) => Client = HashString(clientId);
        public bool ValidateClient(string clientId) => Client == HashString(clientId);

        private string HashString(string s, string? salt = null)
        {
            using (var sha512 = SHA512.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(s + salt);
                var hash = sha512.ComputeHash(bytes);
                var hashStr = Convert.ToHexString(hash) + (salt != null ? $":{salt}" : "");
                return Convert.ToHexString(hash) + (salt != null ? $":{salt}" : "");
            }
        }
    }
}
