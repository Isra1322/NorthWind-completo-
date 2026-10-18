using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCustomerInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Customers",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification",
                table: "Customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Customers",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);

            // Update only placeholder names. Never replace a meaningful customer name or balance.
            migrationBuilder.Sql("""
                UPDATE dbo.Customers SET
                    Name = CASE Id
                        WHEN 'ALFKI' THEN N'Comercial Alfa'
                        WHEN 'ANATR' THEN N'Distribuidora Anatra'
                        WHEN 'BONAP' THEN N'Comercial Bonaparte' END,
                    Identification = COALESCE(Identification, N'DEMO-' + RTRIM(Id)),
                    Email = COALESCE(Email, LOWER(RTRIM(Id)) + N'@example.invalid'),
                    Address = COALESCE(Address, N'Dirección ficticia de demostración')
                WHERE Id IN ('ALFKI', 'ANATR', 'BONAP')
                    AND (Name = Id OR LEN(LTRIM(RTRIM(Name))) = 0);
                """);
            migrationBuilder.AddCheckConstraint("CK_Customers_Email_Format", "Customers", "[Email] IS NULL OR ([Email] LIKE N'%_@_%._%' AND [Email] NOT LIKE N'%[ ' + CHAR(9) + CHAR(10) + CHAR(13) + N']%' AND LEN([Email]) - LEN(REPLACE([Email], N'@', N'')) = 1)");
            migrationBuilder.AddCheckConstraint("CK_Customers_Identification_Format", "Customers", "[Identification] IS NULL OR (LEN([Identification]) BETWEEN 3 AND 30 AND [Identification] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Za-z0-9 -]%' AND LEFT([Identification], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Za-z0-9]')");
            migrationBuilder.AddCheckConstraint("CK_Customers_Phone_Format", "Customers", "[Phone] IS NULL OR ([Phone] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9 ()+-]%' AND LEFT([Phone], 1) LIKE N'[+0-9]' AND CHARINDEX(N'+', [Phone]) IN (0, 1) AND LEN([Phone]) - LEN(REPLACE([Phone], N'+', N'')) <= 1 AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Phone], N'+', N''), N' ', N''), N'(', N''), N')', N''), N'-', N'')) BETWEEN 6 AND 15)");
            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_Name_NotBlank",
                table: "Customers",
                sql: "LEN(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE([Name], CHAR(9), ''), CHAR(10), ''), CHAR(13), '')))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint("CK_Customers_Email_Format", "Customers");
            migrationBuilder.DropCheckConstraint("CK_Customers_Identification_Format", "Customers");
            migrationBuilder.DropCheckConstraint("CK_Customers_Phone_Format", "Customers");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_Name_NotBlank",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Identification",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Customers");
        }
    }
}
