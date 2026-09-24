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

            // Existing invoices keep their INV-{Id} number: the journal (SourceRef),
            // evaluations (TransactionRef) and audit refer to it. New ones get AP-{year}-{number} from the counter.
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
