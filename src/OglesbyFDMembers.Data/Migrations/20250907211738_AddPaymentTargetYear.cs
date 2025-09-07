using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OglesbyFDMembers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTargetYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetYear",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TargetYear",
                table: "Payments",
                column: "TargetYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_TargetYear",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetYear",
                table: "Payments");
        }
    }
}
