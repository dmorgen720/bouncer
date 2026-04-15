using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedInAutoReply.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscarded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Discarded",
                table: "RecruiterMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discarded",
                table: "RecruiterMessages");
        }
    }
}
