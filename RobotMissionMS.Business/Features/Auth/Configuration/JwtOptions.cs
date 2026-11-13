using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace RobotMissionMS.Business.Features.Auth.Configuration;

/// <summary>
/// Settings from the Jwt section. Binding and startup validation will be
/// registered separately when Auth is connected to the API.
/// </summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded random key containing at least 32 bytes for HS256.
    /// Supply through User Secrets or environment variables, never source code.
    /// Validation checks format and length, not whether the key was generated randomly.
    /// </summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    // Default agreed lifetime: 30 minutes. Configuration accepts 1 minute to 1 day.
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Required handles null, empty and whitespace-only values.
        if (string.IsNullOrWhiteSpace(SigningKey))
            yield break;

        byte[]? keyBytes = null;
        var validKey = false;
        try
        {
            keyBytes = Convert.FromBase64String(SigningKey);
            validKey = keyBytes.Length >= 32;
        }
        catch (FormatException)
        {
            // Report the configuration key, never the secret value.
        }
        finally
        {
            if (keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
        }

        if (!validKey)
        {
            yield return new ValidationResult(
                "Jwt:SigningKey must be valid Base64 representing at least 32 bytes.",
                [nameof(SigningKey)]);
        }
    }
}
