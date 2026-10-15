using AppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTemplate.Infrastructure.Persistence.Configurations;

internal sealed class StoredRefreshTokenConfiguration : IEntityTypeConfiguration<StoredRefreshToken>
{
    // Lowercase hex of SHA-256.
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<StoredRefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        // Looked up by hash on every refresh and logout; unique because a hash names exactly one token.
        builder.Property(token => token.TokenHash).HasMaxLength(TokenHashLength);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Revoking a family touches every live token in it.
        builder.HasIndex(token => token.FamilyId);

        // The token belongs to its user: deleting the user deletes its tokens (ADR 0031). No navigation — the token
        // never needs the user, only its id.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Two rotations of one token at the same instant must not both succeed (Postgres xmin).
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(token => token.IsRevoked);
    }
}
