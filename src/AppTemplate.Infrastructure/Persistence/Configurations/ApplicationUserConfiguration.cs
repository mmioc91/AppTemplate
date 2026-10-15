using AppTemplate.Application.Auth;
using AppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTemplate.Infrastructure.Persistence.Configurations;

// On top of Identity's own mapping (IdentityDbContext.OnModelCreating): our column, and ids from the app.
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.DisplayName).HasMaxLength(AuthRules.DisplayNameMaxLength);
    }
}
