using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyTABS.Migrations
{
    /// <inheritdoc />
    public partial class AddCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlbumCoverPath",
                table: "Songs");

            migrationBuilder.AddColumn<byte[]>(
                name: "AlbumCoverData",
                table: "Songs",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlbumCoverData",
                table: "Songs");

            migrationBuilder.AddColumn<string>(
                name: "AlbumCoverPath",
                table: "Songs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
