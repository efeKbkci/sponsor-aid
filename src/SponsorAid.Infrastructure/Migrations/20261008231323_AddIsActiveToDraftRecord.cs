using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorAid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToDraftRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Drafts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_ChatId_IsActive",
                table: "Drafts",
                columns: new[] { "ChatId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drafts_ChatId_IsActive",
                table: "Drafts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Drafts");
        }
    }
}
