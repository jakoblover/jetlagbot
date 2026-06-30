using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JetlagBot.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVouchChannelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VouchChannelId",
                table: "GuildSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VouchChannelId",
                table: "GuildSettings");
        }
    }
}
