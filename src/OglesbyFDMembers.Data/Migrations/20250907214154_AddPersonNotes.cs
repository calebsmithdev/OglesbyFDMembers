using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OglesbyFDMembers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "People",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "People");
        }
    }
}
