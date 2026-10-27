namespace OpenOrderSystem.Core.Services.Keyring.Model;

public class KeyringSecret
{
    public string Key { get; set; } = string.Empty;
    public string Cyphertext { get; set; } = string.Empty;
}