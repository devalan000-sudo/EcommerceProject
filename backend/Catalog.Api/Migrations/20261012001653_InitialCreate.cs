using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ProtectedToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Announcement = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HeroTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeroDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FooterDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#143f35"),
                    BackgroundColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#f7f7f2"),
                    AccentColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false, defaultValue: "#d9e3b4"),
                    FaviconUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, defaultValue: ""),
                    ContactEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, defaultValue: ""),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    BusinessAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    BusinessHours = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WhatsAppPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingPolicy = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false, defaultValue: ""),
                    ReturnsPolicy = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false, defaultValue: ""),
                    InstagramUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, defaultValue: ""),
                    FacebookUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, defaultValue: ""),
                    TikTokUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                    table.CheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExteriorNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InteriorNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    References = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerEmailTokens",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerEmailTokens", x => x.Hash);
                    table.ForeignKey(
                        name: "FK_CustomerEmailTokens_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancellationPending = table.Column<bool>(type: "bit", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationAdminId = table.Column<int>(type: "int", nullable: true),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ShippingOptionId = table.Column<int>(type: "int", nullable: true),
                    ShippingName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StripeSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StripeFrontendUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripeHold = table.Column<bool>(type: "bit", nullable: false),
                    PaymentDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FulfillmentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FulfillmentVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManualPaymentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManualPaymentRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ManualPaymentConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManualPaymentAdminId = table.Column<int>(type: "int", nullable: true),
                    ManualPaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ManualPaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RefundRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RefundReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefundRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefundAdminId = table.Column<int>(type: "int", nullable: true),
                    RefundStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StripeRefundId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RefundPaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RefundRestock = table.Column<bool>(type: "bit", nullable: false),
                    RefundedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<int>(type: "int", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Released = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ReturnRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ManualReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StripeRefundId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRefunds", x => x.Id);
                    table.CheckConstraint("CK_ReturnRefund_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_ReturnRefunds_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequestItems",
                columns: table => new
                {
                    ReturnRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    RestockedQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequestItems", x => new { x.ReturnRequestId, x.OrderItemId });
                    table.CheckConstraint("CK_ReturnItem_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_ReturnItem_Received", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [RestockedQuantity] >= 0 AND [RestockedQuantity] <= [ReceivedQuantity]");
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItems_ReturnRequests_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "ReturnRefundItems",
                columns: table => new
                {
                    ReturnRefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRefundItems", x => new { x.ReturnRefundId, x.OrderItemId });
                    table.CheckConstraint("CK_ReturnRefundItem_Quantity", "[Quantity] > 0 AND [UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_ReturnRefundItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRefundItems_ReturnRefunds_ReturnRefundId",
                        column: x => x.ReturnRefundId,
                        principalTable: "ReturnRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 1, "Nuestro catálogo", "General" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId_IsDefault",
                table: "CustomerAddresses",
                columns: new[] { "CustomerId", "IsDefault" },
                unique: true,
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEmailTokens_CustomerId_Purpose_CreatedAtUtc",
                table: "CustomerEmailTokens",
                columns: new[] { "CustomerId", "Purpose", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedEmail",
                table: "Customers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_EventKey",
                table: "EmailOutbox",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptUtc",
                table: "EmailOutbox",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId_VariantId",
                table: "InventoryReservations",
                columns: new[] { "OrderId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_VariantId_ExpiresAtUtc",
                table: "InventoryReservations",
                columns: new[] { "VariantId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_ClientRequestId",
                table: "Orders",
                columns: new[] { "CustomerId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Number",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripeRefundId",
                table: "Orders",
                column: "StripeRefundId",
                unique: true,
                filter: "[StripeRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripeSessionId",
                table: "Orders",
                column: "StripeSessionId",
                unique: true,
                filter: "[StripeSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Size_Color",
                table: "ProductVariants",
                columns: new[] { "ProductId", "Size", "Color" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReceiptItems_OrderItemId",
                table: "ReturnReceiptItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnReceipts_ReturnRequestId_ClientRequestId",
                table: "ReturnReceipts",
                columns: new[] { "ReturnRequestId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRefundItems_OrderItemId",
                table: "ReturnRefundItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRefunds_ReturnRequestId_ClientRequestId",
                table: "ReturnRefunds",
                columns: new[] { "ReturnRequestId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRefunds_Status_CreatedAtUtc",
                table: "ReturnRefunds",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRefunds_StripeRefundId",
                table: "ReturnRefunds",
                column: "StripeRefundId",
                unique: true,
                filter: "[StripeRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItems_OrderItemId",
                table: "ReturnRequestItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_OrderId_ClientRequestId",
                table: "ReturnRequests",
                columns: new[] { "OrderId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_Status_CreatedAtUtc",
                table: "ReturnRequests",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "CustomerEmailTokens");

            migrationBuilder.DropTable(
                name: "EmailOutbox");

            migrationBuilder.DropTable(
                name: "InventoryReservations");

            migrationBuilder.DropTable(
                name: "ReturnReceiptItems");

            migrationBuilder.DropTable(
                name: "ReturnRefundItems");

            migrationBuilder.DropTable(
                name: "ReturnRequestItems");

            migrationBuilder.DropTable(
                name: "ShippingOptions");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ReturnReceipts");

            migrationBuilder.DropTable(
                name: "ReturnRefunds");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "ReturnRequests");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
