using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawStash.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CreateFileSystemItemsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_system_items",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    parent_folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name_lowercase = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, computedColumnSql: "lower(name)", stored: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    item_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    link_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    link_description = table.Column<string>(type: "text", nullable: true),
                    note_text = table.Column<string>(type: "text", nullable: true),
                    file_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    file_mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_system_items", x => x.item_id);
                    table.CheckConstraint("ck_file_system_items_item_type", "item_type IN ('folder', 'link', 'note', 'photo', 'document')");
                    table.CheckConstraint("ck_file_system_items_link_url", "item_type <> 'link' OR link_url IS NOT NULL");
                    table.CheckConstraint("ck_file_system_items_note_text", "item_type <> 'note' OR note_text IS NOT NULL");
                    table.CheckConstraint("ck_file_system_items_uploaded_file", "item_type NOT IN ('photo', 'document') OR (file_path IS NOT NULL AND file_mime_type IS NOT NULL AND file_size_bytes IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_file_system_items_owner",
                        column: x => x.owner_email,
                        principalTable: "users",
                        principalColumn: "email",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_file_system_items_parent_folder",
                        column: x => x.parent_folder_id,
                        principalTable: "file_system_items",
                        principalColumn: "item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_system_items_parent_folder_id",
                table: "file_system_items",
                column: "parent_folder_id");

            migrationBuilder.CreateIndex(
                name: "uq_file_system_items_name_in_folder",
                table: "file_system_items",
                columns: new[] { "owner_email", "parent_folder_id", "name_lowercase" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_system_items");
        }
    }
}
