using System.Security.Cryptography;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.OrderNumbers;

/// <summary>
/// Default scoped implementation of <see cref="IConfirmationNumberCodec"/>: a four-round Feistel network over
/// two 4-digit halves, keyed with HMAC-SHA256. The key lives in the config store and is generated and locked
/// on first use.
/// </summary>
public class ConfirmationNumberCodec : IConfirmationNumberCodec
{
    private const int HalfModulus = 10_000;   // two halves of 0–9999 cover the 8-digit range
    private const int Rounds = 4;

    private readonly IConfigurationStore _config;
    private byte[]? _key;

    public ConfirmationNumberCodec(IConfigurationStore config)
    {
        _config = config;
    }

    public async Task<int> ToConfirmationNumberAsync(int orderNumber)
    {
        ValidateRange(orderNumber, nameof(orderNumber));
        var key = await GetKeyAsync();
        int left = orderNumber / HalfModulus, right = orderNumber % HalfModulus;
        for (var round = 0; round < Rounds; round++)
            (left, right) = (right, (left + RoundValue(key, right, round)) % HalfModulus);
        return left * HalfModulus + right;
    }

    public async Task<int> ToOrderNumberAsync(int confirmationNumber)
    {
        ValidateRange(confirmationNumber, nameof(confirmationNumber));
        var key = await GetKeyAsync();
        int left = confirmationNumber / HalfModulus, right = confirmationNumber % HalfModulus;
        for (var round = Rounds - 1; round >= 0; round--)
            (left, right) = ((right - RoundValue(key, left, round) + HalfModulus) % HalfModulus, left);
        return left * HalfModulus + right;
    }

    private static int RoundValue(byte[] key, int half, int round)
    {
        Span<byte> input = stackalloc byte[5];
        BitConverter.TryWriteBytes(input, half);
        input[4] = (byte)round;
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(key, input, hash);
        return (int)(BitConverter.ToUInt32(hash) % HalfModulus);
    }

    private async Task<byte[]> GetKeyAsync()
    {
        if (_key is not null)
            return _key;

        var stored = await _config.GetConfigurationAsync<string>(OrderingConfigKeys.ConfirmationKey);
        if (stored is null)
        {
            // Locked so it is never overwritten: a new key would orphan every issued confirmation number.
            // If another request generated one first, the set is refused and the re-read picks theirs up.
            await _config.SetConfigurationAsync(OrderingConfigKeys.ConfirmationKey,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                new IConfigurationStore.SetConfigOptions(setLocked: true));
            stored = await _config.GetConfigurationAsync<string>(OrderingConfigKeys.ConfirmationKey)
                     ?? throw new InvalidOperationException("Failed to create the confirmation number key.");
        }
        return _key = Convert.FromBase64String(stored);
    }

    private static void ValidateRange(int value, string paramName)
    {
        if (value is < 0 or >= HalfModulus * HalfModulus)
            throw new ArgumentOutOfRangeException(paramName, value, "Must be between 0 and 99,999,999.");
    }
}
