using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedInAutoReply.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptDeclineDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DraftReply",
                table: "RecruiterMessages",
                newName: "DeclineDraft");

            migrationBuilder.AddColumn<string>(
                name: "AcceptDraft",
                table: "RecruiterMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptDraft",
                table: "RecruiterMessages");

            migrationBuilder.RenameColumn(
                name: "DeclineDraft",
                table: "RecruiterMessages",
                newName: "DraftReply");
        }
    }
}
