using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud24_25.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class added_size_to_file_revision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "FileRevisions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "FileRevisions");
        }
    }
}
