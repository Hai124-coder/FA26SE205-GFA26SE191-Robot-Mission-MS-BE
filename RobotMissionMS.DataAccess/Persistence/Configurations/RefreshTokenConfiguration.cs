using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RobotMissionMS.DataAccess.Entities;

namespace RobotMissionMS.DataAccess.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("RefreshTokens", "public", table =>
        {
            table.HasCheckConstraint("RefreshTokens_TokenHash_check", """
                "TokenHash" ~ '^[0-9A-F]{64}$'
                """);
            table.HasCheckConstraint("RefreshTokens_ExpiresAt_check", """
                "ExpiresAt" > "CreatedAt"
                """);
            table.HasCheckConstraint("RefreshTokens_ReplacementRevoked_check", """
                "ReplacedByTokenId" IS NULL OR "RevokedAt" IS NOT NULL
                """);
        });

        entity.HasKey(x => x.Id).HasName("RefreshTokens_pkey");
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        entity.Property(x => x.ExpiresAt).HasColumnType("timestamp with time zone");
        entity.Property(x => x.RevokedAt).HasColumnType("timestamp with time zone");

        entity.HasIndex(x => x.TokenHash, "RefreshTokens_TokenHash_key").IsUnique();
        entity.HasIndex(x => new { x.UserId, x.FamilyId }, "RefreshTokens_UserId_FamilyId_idx");
        entity.HasIndex(x => x.ExpiresAt, "RefreshTokens_ExpiresAt_idx");

        entity.HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("RefreshTokens_UserId_fkey");

        entity.HasOne(x => x.ReplacedByToken)
            .WithMany(x => x.InverseReplacedByToken)
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("RefreshTokens_ReplacedByTokenId_fkey");
    }
}
