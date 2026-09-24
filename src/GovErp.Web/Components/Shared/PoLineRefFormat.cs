namespace GovErp.Web.Components.Shared;

/// <summary>Parses "NUMBER/lineNo" (PurchaseOrder.LineRef, Encumbrance.PoLineRef) for a link to the purchase order card.</summary>
public static class PoLineRefFormat
{
    public static (string Number, int? LineNo) Split(string poLineRef)
    {
        var idx = poLineRef.LastIndexOf('/');
        return idx <= 0 ? (poLineRef, null) : (poLineRef[..idx], int.TryParse(poLineRef[(idx + 1)..], out var n) ? n : null);
    }
}
