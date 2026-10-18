using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NorthWind.Sales.Backend.DataContexts.EFCore.DataContexts;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Migrations;

[DbContext(typeof(NorthWindContext))]
[Migration("20261015140000_AddOrderRequestId")]
public class AddOrderRequestId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "RequestId", table: "Orders", type: "uniqueidentifier", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Orders_RequestId", table: "Orders", column: "RequestId",
            unique: true, filter: "[RequestId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Orders_RequestId", table: "Orders");
        migrationBuilder.DropColumn(name: "RequestId", table: "Orders");
    }
}
