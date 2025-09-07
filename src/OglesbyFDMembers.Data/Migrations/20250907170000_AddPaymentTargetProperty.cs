using Microsoft.EntityFrameworkCore.Migrations;

namespace OglesbyFDMembers.Data.Migrations
{
    public partial class AddPaymentTargetProperty : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetPropertyId",
                table: "Payments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TargetPropertyId",
                table: "Payments",
                column: "TargetPropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Properties_TargetPropertyId",
                table: "Payments",
                column: "TargetPropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Properties_TargetPropertyId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TargetPropertyId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TargetPropertyId",
                table: "Payments");
        }
    }
}

