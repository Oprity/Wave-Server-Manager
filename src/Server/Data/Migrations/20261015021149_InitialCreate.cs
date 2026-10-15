using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcServerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TcpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UdpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    HttpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxClients = table.Column<int>(type: "INTEGER", nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AdminPassword = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Track = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TrackLayout = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CarsCsv = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    PickupModeEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoopMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRestartOnCrash = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Name",
                table: "Profiles",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
