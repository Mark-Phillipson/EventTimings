using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventTimings.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Officials",
                columns: table => new
                {
                    OfficialId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PinHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PinSalt = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Officials", x => x.OfficialId);
                });

            migrationBuilder.CreateTable(
                name: "RouteTypes",
                columns: table => new
                {
                    RouteTypeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DistanceMiles = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTypes", x => x.RouteTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Riders",
                columns: table => new
                {
                    RiderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BibNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RouteTypeId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riders", x => x.RiderId);
                    table.ForeignKey(
                        name: "FK_Riders_RouteTypes_RouteTypeId",
                        column: x => x.RouteTypeId,
                        principalTable: "RouteTypes",
                        principalColumn: "RouteTypeId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Officials_FullName",
                table: "Officials",
                column: "FullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Riders_BibNumber",
                table: "Riders",
                column: "BibNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Riders_RouteTypeId",
                table: "Riders",
                column: "RouteTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTypes_Name",
                table: "RouteTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Officials");

            migrationBuilder.DropTable(
                name: "Riders");

            migrationBuilder.DropTable(
                name: "RouteTypes");
        }
    }
}
