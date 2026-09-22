namespace GovErp.Application.Web.Reference.Contracts;

public sealed record PurchaseOrderLineVm(int LineNo, string Account, decimal Amount, decimal AuthorizedPoAmount, decimal AlreadyPostedAgainstPo, decimal RemainingEncumbrance);

public sealed record PurchaseOrderVm(string Number, Guid VendorId, string Status, IReadOnlyList<PurchaseOrderLineVm> Lines);
