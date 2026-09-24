using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GovErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                schema: "ap",
                table: "VendorInvoices",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "ap",
                table: "VendorInvoices");
        }
    }
}
