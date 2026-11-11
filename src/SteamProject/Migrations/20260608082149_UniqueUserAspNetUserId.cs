using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamProject.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserAspNetUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove any pre-existing duplicate domain rows for the same identity (keep the lowest Id) so
            // the unique index can be created — a provisioning race could have created more than one.
            migrationBuilder.Sql(@"
                DELETE u FROM [User] u
                INNER JOIN (
                    SELECT MIN(Id) AS KeepId, AspNetUserId
                    FROM [User]
                    WHERE AspNetUserId IS NOT NULL
                    GROUP BY AspNetUserId
                ) keep ON u.AspNetUserId = keep.AspNetUserId
                WHERE u.Id <> keep.KeepId;");

            migrationBuilder.CreateIndex(
                name: "IX_User_AspNetUserId",
                table: "User",
                column: "AspNetUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_AspNetUserId",
                table: "User");
        }
    }
}
