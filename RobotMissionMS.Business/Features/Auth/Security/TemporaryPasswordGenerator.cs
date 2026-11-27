using System.Security.Cryptography;

namespace RobotMissionMS.Business.Features.Auth.Security;

public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public string Generate()
    {
        Span<char> password = stackalloc char[10];
        for (var index = 0; index < password.Length; index++)
            password[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(password);
    }
}
