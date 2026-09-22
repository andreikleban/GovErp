namespace GovErp.Domain.Validation.ValueObjects;

public enum ValidationStep { RequiredSegments = 1, ValidCombination, FundAndGrantRestrictions, TransactionPurpose, BudgetAvailability, EncumbranceImpact, ApprovalRequirements, PostingEligibility }
