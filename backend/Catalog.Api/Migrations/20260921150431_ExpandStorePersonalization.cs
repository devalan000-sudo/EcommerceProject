using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandStorePersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "StoreSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#d9e3b4");

            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "StoreSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#f7f7f2");

            migrationBuilder.AddColumn<string>(
                name: "BusinessAddress",
                table: "StoreSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessHours",
                table: "StoreSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "StoreSettings",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "StoreSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "StoreSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                table: "StoreSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "StoreSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "StoreSettings",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#143f35");

            migrationBuilder.AddColumn<string>(
                name: "ReturnsPolicy",
                table: "StoreSettings",
                type: "nvarchar(max)",
                maxLength: 6000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShippingPolicy",
                table: "StoreSettings",
                type: "nvarchar(max)",
                maxLength: 6000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TikTokUrl",
                table: "StoreSettings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "BusinessAddress",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "BusinessHours",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ReturnsPolicy",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ShippingPolicy",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "TikTokUrl",
                table: "StoreSettings");
        }
    }
}
