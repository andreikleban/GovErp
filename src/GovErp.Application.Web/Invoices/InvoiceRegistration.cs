using GovErp.Application.Web.Common;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;
using GovErp.Domain.Payables.Repositories;

namespace GovErp.Application.Web.Invoices;

/// <summary>
/// Registers a new draft: issues the AP registration number and builds the aggregate. The counter row stays locked until
/// commit, so this is the last step of the command; any failure here is thrown and rolls the number back (no gaps).
/// </summary>
public sealed class InvoiceRegistration(IInvoiceNumbering numbering, IVendorInvoiceRepository invoices, IClock clock)
{
    public async Task<VendorInvoice> RegisterAsync(CreateInvoiceCommand cmd, UserId author, CancellationToken ct)
    {
        var fiscalYear = FiscalYear.FromDate(cmd.PostingDate);
        var sequence = await numbering.NextAsync(fiscalYear, ct);
        var vendorNumber = cmd.GeneratedNumberPrefix is { } prefix ? IInvoiceNumbering.GeneratedNumber(prefix, sequence) : cmd.Number;

        // The constructor and AddDistribution check invariants; dates outside rule 6 are rejected by the pipeline (VALIDATION_INPUT).
        var invoice = new VendorInvoice(vendorNumber, cmd.VendorId, cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate, cmd.DueDate,
            Money.Of(cmd.Total), cmd.PoRef, author, clock.Now, IInvoiceNumbering.Reference(fiscalYear, sequence));
        foreach (var line in cmd.Distributions)
        {
            invoice.AddDistribution(AccountCode.Parse(line.Account), Money.Of(line.Amount), line.PoLineNo);
        }

        // A generated vendor number can collide with one entered by hand earlier; the counter has moved, so throw.
        if (cmd.GeneratedNumberPrefix is not null
            && await invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedInvoiceNumber, null, ct))
        {
            throw new PayablesException(PayablesErrors.DuplicateNumber);
        }

        return invoice;
    }
}
