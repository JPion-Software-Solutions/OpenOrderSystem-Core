using OpenOrderSystem.Core.Services.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace OpenOrderSystem.Core.Services
{
    public class TwilioSmsService : ISmsService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _phoneNumber;
        private ILogger<TwilioSmsService> _logger;

        public TwilioSmsService(ILogger<TwilioSmsService> logger)
        {
            _logger = logger;

            //retrieve Twilio credentials
            _accountSid = Environment.GetEnvironmentVariable("TWILIO_SID") ?? "ERROR";
            _authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH") ?? "ERROR";
            _phoneNumber = Environment.GetEnvironmentVariable("TWILIO_PHONE") ?? "ERROR";
        }

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
            if (_accountSid == "ERROR" || _authToken == "ERROR" || _phoneNumber == "ERROR")
            {
                //mask auth token
                var mask = string.Empty;
                for (int i = 0; i < _authToken.Length - 4; i++) mask += "*";
                var maskedAuth = _authToken == "ERROR" ? "ERROR" : $"{mask}{_authToken.Substring(_authToken.Length - 4)}";

                _logger.LogError("Failed to load Twilio info");
                _logger.LogError($"AccountId: {_accountSid}");
                _logger.LogError($"AuthToken: {maskedAuth}");
                _logger.LogError($"Sender Phone: {_phoneNumber}");
            }
            else
            {
                TwilioClient.Init(_accountSid, _authToken);

                var messageResource = MessageResource.Create(
                    from: new PhoneNumber(_phoneNumber),
                    to: new PhoneNumber(phoneNumber),
                    body: message);

                if (messageResource.Status == MessageResource.StatusEnum.Failed)
                {
                    _logger.LogError($"SMS failed to send with error the following error {messageResource.ErrorCode}: {messageResource.ErrorMessage}");
                }
                else
                {
                    _logger.LogInformation($"SMS message sent successfully with status: {messageResource.Status}");
                }
            }
        }

        public bool VerifyPhone(string phoneNumber)
        {
            return true;
        }

    }
}
