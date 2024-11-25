using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud24_25.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Changed_files : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Extension",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "BucketName",
                table: "FileRevisions");

            migrationBuilder.RenameColumn(
                name: "Path",
                table: "Files",
                newName: "ContentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ContentType",
                table: "Files",
                newName: "Path");

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                table: "Files",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                table: "FileRevisions",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
