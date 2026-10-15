using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcServerManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutableArgs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutableArgs",
                table: "Profiles",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutableArgs",
                table: "Profiles");
        }
    }
}
