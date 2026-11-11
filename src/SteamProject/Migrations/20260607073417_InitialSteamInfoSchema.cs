using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamProject.Migrations
{
    /// <inheritdoc />
    public partial class InitialSteamInfoSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUser",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ASPNetIdentityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__AdminUse__3214EC27888529AA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Badge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Badge__3214EC07EDD14BDF", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlackList",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SteamId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__BlackLis__3214EC0719BC2BE5", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DescShort = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescLong = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PlayTime = table.Column<int>(type: "int", nullable: true),
                    IconUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastPlayed = table.Column<int>(type: "int", nullable: true),
                    Genres = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Categories = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Game__3214EC07DDFB349B", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameAchievement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PointVal = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    ApiName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IconAchievedUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IconHiddenUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    HiddenFromUsers = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GameAchi__3214EC07341BB293", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IGDBGenres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__IGDBGenr__3214EC075DD32ED1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Status__3214EC07C7EE4060", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AspNetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SteamId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SteamName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProfileUrl = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PersonaState = table.Column<int>(type: "int", nullable: true),
                    PlayerLevel = table.Column<int>(type: "int", nullable: true),
                    Theme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__User__3214EC0761BB6D0B", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Competition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatorId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Competit__3214EC0759C3A4E2", x => x.Id);
                    table.ForeignKey(
                        name: "Competition_Fk_Game",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Competition_Fk_Status",
                        column: x => x.StatusId,
                        principalTable: "Status",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "Competition_Fk_User",
                        column: x => x.CreatorId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Friend",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RootId = table.Column<int>(type: "int", nullable: false),
                    SteamId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SteamName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PersonaState = table.Column<int>(type: "int", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvatarFullUrl = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastLogOff = table.Column<int>(type: "int", nullable: true),
                    GameExtraInfo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    Nickname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Linked = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Friend__3214EC0797F84742", x => x.Id);
                    table.ForeignKey(
                        name: "Friend_Fk_User",
                        column: x => x.RootId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameVote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Vote = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GameVote__3214EC07E45E3C39", x => x.Id);
                    table.ForeignKey(
                        name: "GameVote_Fk_Game",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "GameVote_Fk_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "datetime", nullable: true),
                    Sender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__InboxMes__3214EC07597D7312", x => x.Id);
                    table.ForeignKey(
                        name: "InboxMessage_Fk_User",
                        column: x => x.RecipientId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserAchievement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    AchievementId = table.Column<int>(type: "int", nullable: false),
                    Achieved = table.Column<bool>(type: "bit", nullable: false),
                    UnlockTime = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserAchi__3214EC076928D835", x => x.Id);
                    table.ForeignKey(
                        name: "UserAchievement_FK_Achievement",
                        column: x => x.AchievementId,
                        principalTable: "GameAchievement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "UserAchievement_Fk_User",
                        column: x => x.OwnerId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserBadge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BadgeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserBadg__3214EC07D68B9855", x => x.Id);
                    table.ForeignKey(
                        name: "UserBadge_Fk_Badge",
                        column: x => x.BadgeId,
                        principalTable: "Badge",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "UserBadge_Fk_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserGameInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayTime = table.Column<int>(type: "int", nullable: true),
                    LastPlayed = table.Column<int>(type: "int", nullable: true),
                    Hidden = table.Column<bool>(type: "bit", nullable: false),
                    Followed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__UserGame__3214EC073C0D35AE", x => x.Id);
                    table.ForeignKey(
                        name: "UserGameInfo_FK_Game",
                        column: x => x.GameId,
                        principalTable: "Game",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "UserGameInfo_FK_User",
                        column: x => x.OwnerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionGameAchievement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    GameAchievementId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Competit__3214EC077DC18BA3", x => x.Id);
                    table.ForeignKey(
                        name: "CompetitionGameAchievement_Fk_Competition",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPlayer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    SteamId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Competit__3214EC07D984ECCF", x => x.Id);
                    table.ForeignKey(
                        name: "CompetitionPlayer_Fk_Competition",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionVote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WantsToPlayAgain = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Competit__3214EC0703135BBE", x => x.Id);
                    table.ForeignKey(
                        name: "CompetitionVote_Fk_Competition",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "CompetitionVote_Fk_User",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_CreatorId",
                table: "Competition",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_GameId",
                table: "Competition",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_StatusId",
                table: "Competition",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionGameAchievement_CompetitionId",
                table: "CompetitionGameAchievement",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayer_CompetitionId",
                table: "CompetitionPlayer",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionVote_CompetitionId",
                table: "CompetitionVote",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionVote_UserId",
                table: "CompetitionVote",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friend_RootId",
                table: "Friend",
                column: "RootId");

            migrationBuilder.CreateIndex(
                name: "IX_GameVote_GameId",
                table: "GameVote",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameVote_UserId",
                table: "GameVote",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessage_RecipientId",
                table: "InboxMessage",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_AchievementId",
                table: "UserAchievement",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievement_OwnerId",
                table: "UserAchievement",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_BadgeId",
                table: "UserBadge",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadge_UserId",
                table: "UserBadge",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameInfo_GameId",
                table: "UserGameInfo",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameInfo_OwnerId",
                table: "UserGameInfo",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUser");

            migrationBuilder.DropTable(
                name: "BlackList");

            migrationBuilder.DropTable(
                name: "CompetitionGameAchievement");

            migrationBuilder.DropTable(
                name: "CompetitionPlayer");

            migrationBuilder.DropTable(
                name: "CompetitionVote");

            migrationBuilder.DropTable(
                name: "Friend");

            migrationBuilder.DropTable(
                name: "GameVote");

            migrationBuilder.DropTable(
                name: "IGDBGenres");

            migrationBuilder.DropTable(
                name: "InboxMessage");

            migrationBuilder.DropTable(
                name: "UserAchievement");

            migrationBuilder.DropTable(
                name: "UserBadge");

            migrationBuilder.DropTable(
                name: "UserGameInfo");

            migrationBuilder.DropTable(
                name: "Competition");

            migrationBuilder.DropTable(
                name: "GameAchievement");

            migrationBuilder.DropTable(
                name: "Badge");

            migrationBuilder.DropTable(
                name: "Game");

            migrationBuilder.DropTable(
                name: "Status");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
