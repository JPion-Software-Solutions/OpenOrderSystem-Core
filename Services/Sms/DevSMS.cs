using OpenOrderSystem.Core.Services.Sms.Interfaces;

namespace OpenOrderSystem.Core.Services.Sms
{
    public class DevSMS : ISmsService
    {
        public string ConvertPhone(string phoneNumber)
        {
            var number = "+1";

            foreach (var digit in phoneNumber)
            {
                if (char.IsDigit(digit)) number += digit;
            }

            return number;
        }

        public void SendSMS(string phoneNumber, string message, dynamic? additionalSettings = null)
        {
            Console.WriteLine("***************** OUTGOING SMS MESSAGE *****************");
            Console.WriteLine($"\t  PHONE: {phoneNumber} ");
            Console.WriteLine($"\tMESSAGE: {message} ");
        }

        public bool VerifyPhone(string phoneNumber)
        {
            return true;
        }


    }
}
