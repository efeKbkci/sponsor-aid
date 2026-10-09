using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorAid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationTrackingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationTrackings",
                columns: table => new
                {
                    RegistrationKey = table.Column<string>(type: "text", nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTrackings", x => x.RegistrationKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrationTrackings");
        }
    }
}
