using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NorthWind.Sales.Backend.DataContexts.EFCore.DataContexts;

#nullable disable

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Migrations;

[DbContext(typeof(NorthWindContext))]
[Migration("20261015120000_AddInvoiceVersions")]
public partial class AddInvoiceVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InvoiceVersions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                InvoiceId = table.Column<int>(type: "int", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PdfContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceVersions", x => x.Id);
                table.ForeignKey("FK_InvoiceVersions_Orders_InvoiceId", x => x.InvoiceId, "Orders", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_InvoiceVersions_InvoiceId_VersionNumber", table: "InvoiceVersions", columns: new[] { "InvoiceId", "VersionNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "InvoiceVersions");
}
