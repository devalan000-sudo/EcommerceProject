using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManualWhatsAppPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManualPaymentAdminId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualPaymentConfirmedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualPaymentMethod",
                table: "Orders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualPaymentReference",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualPaymentRequestHash",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManualPaymentRequestId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualPaymentAdminId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualPaymentConfirmedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualPaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualPaymentReference",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualPaymentRequestHash",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ManualPaymentRequestId",
                table: "Orders");
        }
    }
}
