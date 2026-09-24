namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// The eight steps of the validation pipeline.
/// </summary>
public enum ValidationStep { RequiredSegments = 1, ValidCombination, FundAndGrantRestrictions, TransactionPurpose, BudgetAvailability, EncumbranceImpact, ApprovalRequirements, PostingEligibility }
