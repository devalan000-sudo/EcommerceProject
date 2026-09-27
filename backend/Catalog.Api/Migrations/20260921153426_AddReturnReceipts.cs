using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RestockedQuantity",
                table: "ReturnRequestItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReturnReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnReceipts_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnReceiptItems",
                columns: table => new
                {
                    ReturnReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    RestockedQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnReceiptItems", x => new { x.ReturnReceiptId, x.OrderItemId });
                    table.CheckConstraint("CK_ReceiptItem_Quantities", "[ReceivedQuantity] > 0 AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]");
                    table.ForeignKey(
                        name: "FK_ReturnReceiptItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnReceiptItems_ReturnReceipts_ReturnReceiptId",
                        column: x => x.ReturnReceiptId,
                        principalTable: "ReturnReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReturnItem_Received",
                table: "ReturnRequestItems",
                sql: "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReceiptItems_OrderItemId",
                table: "ReturnReceiptItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReceipts_ReturnRequestId_ClientRequestId",
                table: "ReturnReceipts",
                columns: new[] { "ReturnRequestId", "ClientRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnReceiptItems");

            migrationBuilder.DropTable(
                name: "ReturnReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReturnItem_Received",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "ReturnRequestItems");

            migrationBuilder.DropColumn(
                name: "RestockedQuantity",
                table: "ReturnRequestItems");
        }
    }
}
