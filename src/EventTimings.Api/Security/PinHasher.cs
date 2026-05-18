using System.Security.Cryptography;

namespace EventTimings.Api.Security;

internal static class PinHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static (string Hash, string Salt) HashPin(string pin)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool VerifyPin(string pin, string expectedHash, string salt)
    {
        byte[] saltBytes;
        byte[] expectedHashBytes;

        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expectedHashBytes = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, Iterations, HashAlgorithmName.SHA256, expectedHashBytes.Length);
        return CryptographicOperations.FixedTimeEquals(expectedHashBytes, actualHash);
    }
}
