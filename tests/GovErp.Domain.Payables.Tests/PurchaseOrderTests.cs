using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Tests;

public class PurchaseOrderTests
{
    private static readonly AccountCode Account = AccountCode.Parse("701-3000-53100-G-COPS-26");

    [Fact]
    public void Total_and_line_reference_use_authorized_lines()
    {
        var lines = new List<PurchaseOrderLine> { new(1, Account, Money.Of(160000)) };
        var po = new PurchaseOrder("PO-1", Guid.NewGuid(), lines);
        lines.Clear();
        po.Total.Should().Be(Money.Of(160000));
        po.LineRef(1).Should().Be("PO-1/1");
        po.Status.Should().Be(PurchaseOrderStatus.Open);
        FluentActions.Invoking(() => po.LineRef(2)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Empty_duplicate_and_negative_lines_are_rejected()
    {
        FluentActions.Invoking(() => new PurchaseOrder("PO-1", Guid.NewGuid(), [])).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => new PurchaseOrder("PO-1", Guid.NewGuid(),
            [new(1, Account, Money.Of(1)), new(1, Account, Money.Of(2))])).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => new PurchaseOrderLine(1, Account, Money.Of(-1))).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => new PurchaseOrderLine(0, Account, Money.Of(1))).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Vendor_requires_identity_code_and_name()
    {
        FluentActions.Invoking(() => new Vendor(Guid.Empty, "V", "Vendor", VendorStatus.Active, true)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new Vendor(Guid.NewGuid(), " ", "Vendor", VendorStatus.Active, true)).Should().Throw<ArgumentException>();
        var vendor = new Vendor(Guid.NewGuid(), "V", "Vendor", VendorStatus.Debarred, false);
        vendor.Status.Should().Be(VendorStatus.Debarred);
        vendor.SamRegistered.Should().BeFalse();
    }
}
