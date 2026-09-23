namespace GovErp.Web.Components.Shared;

/// <summary>Разбирает "NUMBER/lineNo" (PurchaseOrder.LineRef, Encumbrance.PoLineRef) для ссылки на карточку заказа.</summary>
public static class PoLineRefFormat
{
    public static (string Number, int? LineNo) Split(string poLineRef)
    {
        var idx = poLineRef.LastIndexOf('/');
        return idx <= 0 ? (poLineRef, null) : (poLineRef[..idx], int.TryParse(poLineRef[(idx + 1)..], out var n) ? n : null);
    }
}
