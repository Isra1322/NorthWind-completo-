using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalesRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "Products",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Orders",
                type: "decimal(7,6)",
                precision: 7,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "OrderDetails",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Populate only new columns; preserve existing orders, stock and customer balances.
            migrationBuilder.Sql("""
                UPDATE c SET Name = c.Id FROM Customers c WHERE c.Name = '';
                UPDATE p SET UnitPrice = price.UnitPrice
                FROM Products p
                CROSS APPLY (
                    SELECT TOP (1) d.UnitPrice FROM OrderDetails d
                    JOIN Orders o ON o.Id = d.OrderId
                    WHERE d.ProductId = p.Id AND d.UnitPrice > 0
                    ORDER BY o.OrderDate DESC, o.Id DESC
                ) price
                WHERE p.UnitPrice = 0;
                UPDATE d SET ProductName = p.Name
                FROM OrderDetails d JOIN Products p ON p.Id = d.ProductId
                WHERE d.ProductName IS NULL;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Esta migraci?n no se revierte autom?ticamente para evitar perder precios, importes y datos de facturas.");
        }
    }
}
