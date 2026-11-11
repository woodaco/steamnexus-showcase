using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamProject.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxIsRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "InboxMessage",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "InboxMessage");
        }
    }
}
