using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedInAutoReply.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecruiterMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    GraphMessageId = table.Column<string>(type: "TEXT", nullable: false),
                    EmailSubject = table.Column<string>(type: "TEXT", nullable: false),
                    RecruiterName = table.Column<string>(type: "TEXT", nullable: false),
                    Agency = table.Column<string>(type: "TEXT", nullable: false),
                    HiringCompany = table.Column<string>(type: "TEXT", nullable: true),
                    RoleTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    ContractType = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalMessageText = table.Column<string>(type: "TEXT", nullable: false),
                    ReplyToAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Verdict = table.Column<int>(type: "INTEGER", nullable: false),
                    AssessmentJson = table.Column<string>(type: "TEXT", nullable: false),
                    DraftReply = table.Column<string>(type: "TEXT", nullable: false),
                    ReplyLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Approved = table.Column<bool>(type: "INTEGER", nullable: true),
                    FinalDraft = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruiterMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecruiterMessages");

            migrationBuilder.DropTable(
                name: "SyncStates");
        }
    }
}
