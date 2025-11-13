namespace OpenOrderSystem.Core.Services.Interfaces
{
    public interface ISmsService
    {
        /// <summary>
        /// Send an SMS message using the provided SMS Service Provider
        /// </summary>
        /// <param name="phoneNumber">Message recipient</param>
        /// <param name="message">SMS message content</param>
        /// <param name="additionalSettings">OPTIONAL: allows any additional settings to be 
        /// applied through the generic interface</param>
        public void SendSMS(string phoneNumber, string message, dynamic? additionalSettings = null);

        /// <summary>
        /// Verify a given phone number is valid.
        /// </summary>
        /// <param name="phoneNumber">Phone number to verify</param>
        /// <returns>True upon successful verification</returns>
        public bool VerifyPhone(string phoneNumber);

        /// <summary>
        /// Converts an input phone number to the correct format
        /// </summary>
        /// <param name="phoneNumber">phone number to be converted</param>
        /// <returns>converted phone number</returns>
        public string ConvertPhone(string phoneNumber);
    }
}
