using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BillingEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerUserIdAndPlanSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Customers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Currency", "IsActive", "Name", "Price", "BillingCycle_IntervalDays" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "USD", true, "Basic", 9.99m, 30 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "USD", true, "Pro", 29.99m, 30 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "USD", true, "Enterprise", 299.99m, 365 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId",
                table: "Customers");

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Customers");
        }
    }
}
