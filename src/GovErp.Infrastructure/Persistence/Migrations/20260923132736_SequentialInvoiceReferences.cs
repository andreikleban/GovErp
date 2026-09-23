using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GovErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SequentialInvoiceReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "ap",
                table: "VendorInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            // Уже созданные инвойсы сохраняют прежний номер INV-{Id}: на него ссылаются журнал (SourceRef),
            // оценки (TransactionRef) и аудит. Новые получают AP-{год}-{номер} из счётчика.
            migrationBuilder.Sql("UPDATE [ap].[VendorInvoices] SET [Reference] = N'INV-' + LOWER(CONVERT(nvarchar(36), [Id]));");

            migrationBuilder.CreateTable(
                name: "DocumentCounters",
                schema: "ap",
                columns: table => new
                {
                    Series = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCounters", x => x.Series);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_Reference",
                schema: "ap",
                table: "VendorInvoices",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCounters",
                schema: "ap");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoices_Reference",
                schema: "ap",
                table: "VendorInvoices");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "ap",
                table: "VendorInvoices");
        }
    }
}
