using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawStash.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionInFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "position_in_folder",
                table: "file_system_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE file_system_items AS item
                SET position_in_folder = ranked.position_in_folder
                FROM (
                    SELECT item_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY owner_email, parent_folder_id
                               ORDER BY item_type <> 'folder', name_lowercase) - 1 AS position_in_folder
                    FROM file_system_items) AS ranked
                WHERE item.item_id = ranked.item_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "position_in_folder",
                table: "file_system_items");
        }
    }
}
