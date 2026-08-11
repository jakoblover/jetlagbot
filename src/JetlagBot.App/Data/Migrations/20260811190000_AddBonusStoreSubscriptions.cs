using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JetlagBot.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBonusStoreSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BonusStoreSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscordUserId = table.Column<string>(type: "text", nullable: false),
                    StoreKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StoreDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusStoreSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BonusStoreSubscriptions_DiscordUserId_StoreKey",
                table: "BonusStoreSubscriptions",
                columns: new[] { "DiscordUserId", "StoreKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BonusStoreSubscriptions_StoreKey",
                table: "BonusStoreSubscriptions",
                column: "StoreKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BonusStoreSubscriptions");
        }
    }
}
