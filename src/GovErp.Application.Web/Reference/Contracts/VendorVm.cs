namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// A vendor as shown on screen.
/// </summary>
public sealed record VendorVm(Guid Id, string Code, string Name, string Status, bool SamRegistered);
