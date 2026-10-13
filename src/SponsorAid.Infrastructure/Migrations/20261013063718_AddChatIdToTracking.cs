using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorAid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatIdToTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "RegistrationTrackings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "RegistrationTrackings");
        }
    }
}
