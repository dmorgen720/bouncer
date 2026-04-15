using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedInAutoReply.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "RecruiterMessages");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "RecruiterMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractType",
                table: "RecruiterMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "RecruiterMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
