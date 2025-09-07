using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OglesbyFDMembers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonAliasType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "PersonAliases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "PersonAliases");
        }
    }
}
