using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OglesbyFDMembers.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilityNoticePaymentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "UtilityNotices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilityNotices_PaymentId",
                table: "UtilityNotices",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_UtilityNotices_Payments_PaymentId",
                table: "UtilityNotices",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UtilityNotices_Payments_PaymentId",
                table: "UtilityNotices");

            migrationBuilder.DropIndex(
                name: "IX_UtilityNotices_PaymentId",
                table: "UtilityNotices");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "UtilityNotices");
        }
    }
}
