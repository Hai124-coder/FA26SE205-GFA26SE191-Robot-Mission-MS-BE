using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RobotMissionMS.Business.Features.Auth.Configuration;

namespace RobotMissionMS.Business.Features.Auth.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    // 64 random bytes produce an 88-character Base64 token, within the DTO's 128 limit.
    private const int TokenByteCount = 64;
    private readonly int refreshTokenDays;
    private readonly TimeProvider clock;

    public RefreshTokenGenerator(IOptions<AuthOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        var settings = options.Value;
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(settings, new ValidationContext(settings), errors, validateAllProperties: true))
        {
            throw new OptionsValidationException(AuthOptions.SectionName, typeof(AuthOptions),
                errors.Select(error => error.ErrorMessage ?? "Invalid Auth configuration."));
        }

        refreshTokenDays = settings.RefreshTokenDays;
        this.clock = clock;
    }

    public RefreshTokenResult Generate()
    {
        var createdAt = clock.GetUtcNow().ToUniversalTime();
        var expiresAt = createdAt.AddDays(refreshTokenDays);
        var bytes = RandomNumberGenerator.GetBytes(TokenByteCount);
        try
        {
            var token = Convert.ToBase64String(bytes);
            return new RefreshTokenResult(token, ComputeHash(token), createdAt, expiresAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        // Hash the exact token text, not decoded Base64. Do not trim or normalize it.
        var bytes = Encoding.UTF8.GetBytes(token);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
