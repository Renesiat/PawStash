using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawStash.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionAndCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "link_description",
                table: "file_system_items",
                newName: "description");

            migrationBuilder.AddColumn<string>(
                name: "cover_image_mime_type",
                table: "file_system_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_image_path",
                table: "file_system_items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_system_items_cover_image",
                table: "file_system_items",
                sql: "(cover_image_path IS NULL) = (cover_image_mime_type IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_file_system_items_cover_image",
                table: "file_system_items");

            migrationBuilder.DropColumn(
                name: "cover_image_mime_type",
                table: "file_system_items");

            migrationBuilder.DropColumn(
                name: "cover_image_path",
                table: "file_system_items");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "file_system_items",
                newName: "link_description");
        }
    }
}
