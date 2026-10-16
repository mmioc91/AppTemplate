using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueuedMails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "queued_mails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    sender_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reply_to = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queued_mails", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_queued_mails_next_attempt_at",
                table: "queued_mails",
                column: "next_attempt_at",
                filter: "sent_at IS NULL AND failed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "queued_mails");
        }
    }
}
