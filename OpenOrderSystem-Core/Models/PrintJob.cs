using System.Text.Json.Serialization;

namespace OpenOrderSystem.Core.Models
{
    public class PrintJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();

        public string PrinterId { get; set; } = string.Empty;

        [JsonIgnore]
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public string PrintData => Convert.ToHexString(Data);

        public bool InProgress { get; set; }
    }
}
