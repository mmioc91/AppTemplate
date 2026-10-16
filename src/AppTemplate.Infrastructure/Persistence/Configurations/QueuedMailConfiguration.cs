using AppTemplate.Application.Auth;
using AppTemplate.Application.Mail;
using AppTemplate.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTemplate.Infrastructure.Persistence.Configurations;

internal sealed class QueuedMailConfiguration : IEntityTypeConfiguration<QueuedMail>
{
    public void Configure(EntityTypeBuilder<QueuedMail> builder)
    {
        builder.HasKey(mail => mail.Id);
        builder.Property(mail => mail.Id).ValueGeneratedNever();
        builder.Property(mail => mail.To).HasMaxLength(AuthRules.EmailMaxLength);
        builder.Property(mail => mail.Subject).HasMaxLength(MailRules.SubjectMaxLength);
        builder.Property(mail => mail.SenderName).HasMaxLength(MailRules.SenderNameMaxLength);
        builder.Property(mail => mail.ReplyTo).HasMaxLength(AuthRules.EmailMaxLength);

        // The worker's query: what is still waiting, by when it is due.
        builder.HasIndex(mail => mail.NextAttemptAt).HasFilter("sent_at IS NULL AND failed_at IS NULL");
    }
}
