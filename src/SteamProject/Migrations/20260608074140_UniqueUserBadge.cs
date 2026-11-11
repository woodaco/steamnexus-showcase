using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamProject.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserBadge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserBadge_UserId",
                table: "UserBadge");

            // Remove any pre-existing duplicate (UserId, BadgeId) rows (keeping the lowest Id) so the
            // unique index below can be created — the old check-then-insert could race-produce duplicates.
            migrationBuilder.Sql(@"
                DELETE ub FROM UserBadge ub
                INNER JOIN (
                    SELECT MIN(Id) AS KeepId, UserId, BadgeId
                    FROM UserBadge
                    GROUP BY UserId, BadgeId
                ) keep ON ub.UserId = keep.UserId AND ub.BadgeId = keep.BadgeId
                WHERE ub.Id <> keep.KeepId;");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_UserId_BadgeId",
                table: "UserBadge",
                columns: new[] { "UserId", "BadgeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserBadge_UserId_BadgeId",
                table: "UserBadge");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_UserId",
                table: "UserBadge",
                column: "UserId");
        }
    }
}
