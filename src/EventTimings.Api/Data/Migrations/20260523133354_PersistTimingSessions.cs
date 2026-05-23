using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventTimings.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistTimingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimingSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RiderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RiderName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OfficialName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StoppedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimingSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_TimingSessions_Riders_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Riders",
                        principalColumn: "RiderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimingSessions_RiderId_StoppedAt",
                table: "TimingSessions",
                columns: new[] { "RiderId", "StoppedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TimingSessions_StartedAt",
                table: "TimingSessions",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimingSessions");
        }
    }
}
