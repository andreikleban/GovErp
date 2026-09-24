namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>
/// Ledger refusal codes. The texts live in Messages.resx.
/// </summary>
public static class LedgerErrors
{
    public const string BudgetChangedConcurrently = "LEDGER.BUDGET_CHANGED_CONCURRENTLY";
    public const string InvalidBudgetConfiguration = "LEDGER.INVALID_BUDGET_CONFIGURATION";
    public const string ZeroAmendment = "LEDGER.ZERO_AMENDMENT";
    public const string AmendmentOutsideYear = "LEDGER.AMENDMENT_OUTSIDE_YEAR";
    public const string OpeningBalanceNeedsPristineLine = "LEDGER.OPENING_BALANCE_NEEDS_PRISTINE_LINE";
    public const string LiquidationExceedsEncumbered = "LEDGER.LIQUIDATION_EXCEEDS_ENCUMBERED";
    public const string UnknownReservation = "LEDGER.UNKNOWN_RESERVATION";
    public const string ReservationNotHeld = "LEDGER.RESERVATION_NOT_HELD";
    public const string InvalidPoOpening = "LEDGER.INVALID_PO_OPENING";
    public const string InsufficientEncumbrance = "LEDGER.INSUFFICIENT_ENCUMBRANCE";
    public const string InvalidTolerance = "LEDGER.INVALID_TOLERANCE";
    public const string PoBillingExceedsTolerance = "LEDGER.PO_BILLING_EXCEEDS_TOLERANCE";
    public const string UnknownClaim = "LEDGER.UNKNOWN_CLAIM";
    public const string UnknownBillingClaim = "LEDGER.UNKNOWN_BILLING_CLAIM";
    public const string ClaimNotHeld = "LEDGER.CLAIM_NOT_HELD";
    public const string LiquidationExceedsRemaining = "LEDGER.LIQUIDATION_EXCEEDS_REMAINING";
    public const string EncumbranceNotReleasable = "LEDGER.ENCUMBRANCE_NOT_RELEASABLE";
    public const string MonthOutOfRange = "LEDGER.MONTH_OUT_OF_RANGE";
    public const string PeriodAlreadyClosed = "LEDGER.PERIOD_ALREADY_CLOSED";
    public const string JournalIncomplete = "LEDGER.JOURNAL_INCOMPLETE";
    public const string PeriodClosed = "LEDGER.PERIOD_CLOSED";
    public const string JournalTooShort = "LEDGER.JOURNAL_TOO_SHORT";
    public const string JournalUnbalanced = "LEDGER.JOURNAL_UNBALANCED";
    public const string UnknownFamily = "LEDGER.UNKNOWN_FAMILY";
    public const string NegativeJournalAmount = "LEDGER.NEGATIVE_JOURNAL_AMOUNT";
    public const string DebitOrCredit = "LEDGER.DEBIT_OR_CREDIT";
    public const string AmountNotPositive = "LEDGER.AMOUNT_NOT_POSITIVE";
    public const string InvoiceVersionRequired = "LEDGER.INVOICE_VERSION_REQUIRED";
    public const string OpeningDateOutsideYear = "LEDGER.OPENING_DATE_OUTSIDE_YEAR";
    public const string NegativeOpeningBalance = "LEDGER.NEGATIVE_OPENING_BALANCE";
    public const string BudgetLineNotFound = "LEDGER.BUDGET_LINE_NOT_FOUND";
    public const string BudgetNoLongerAvailable = "LEDGER.BUDGET_NO_LONGER_AVAILABLE";
    public const string EncumbranceNotFound = "LEDGER.ENCUMBRANCE_NOT_FOUND";
    public const string PostingTotalMismatch = "LEDGER.POSTING_TOTAL_MISMATCH";
}
