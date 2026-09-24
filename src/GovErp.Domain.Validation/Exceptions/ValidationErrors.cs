namespace GovErp.Domain.Validation.Exceptions;

/// <summary>
/// Codes of rule-configuration problems (ValidationException, refused as RULE_CONFIGURATION).
/// </summary>
public static class ValidationErrors
{
    public const string DefinitionInvalid = "RULES.DEFINITION_INVALID";
    public const string DefinitionEnumInvalid = "RULES.DEFINITION_ENUM_INVALID";
    public const string ParameterMissing = "RULES.PARAMETER_MISSING";
    public const string ParameterNotDecimal = "RULES.PARAMETER_NOT_DECIMAL";
    public const string ParameterNotPositiveAmount = "RULES.PARAMETER_NOT_POSITIVE_AMOUNT";
    public const string ParameterNotShare = "RULES.PARAMETER_NOT_SHARE";
    public const string AmbiguousDefinitions = "RULES.AMBIGUOUS_DEFINITIONS";
    public const string LayerSwitchesOff = "RULES.LAYER_SWITCHES_OFF";
    public const string LayerMovesStep = "RULES.LAYER_MOVES_STEP";
    public const string LayerLowersSeverity = "RULES.LAYER_LOWERS_SEVERITY";
    public const string LayerAddsOverriders = "RULES.LAYER_ADDS_OVERRIDERS";
    public const string LayerChangesParameterSet = "RULES.LAYER_CHANGES_PARAMETER_SET";
    public const string LayerChangesFixedParameter = "RULES.LAYER_CHANGES_FIXED_PARAMETER";
    public const string LayerChangesUndeclaredParameter = "RULES.LAYER_CHANGES_UNDECLARED_PARAMETER";
    public const string LayerRelaxesParameter = "RULES.LAYER_RELAXES_PARAMETER";
    public const string NoSetForScope = "RULES.NO_SET_FOR_SCOPE";
    public const string MultipleScopes = "RULES.MULTIPLE_SCOPES";
    public const string RulesWithoutCode = "RULES.WITHOUT_CODE";
    public const string MandatoryRulesMissing = "RULES.MANDATORY_MISSING";
    public const string FactNeedsName = "RULES.FACT_NEEDS_NAME";
}
