namespace GovErp.Application.Web.Common;

/// <summary>
/// Each context has its own role enum with the same names (EnumMappingTests compares the sets).
/// </summary>
public static class RoleMapping
{
    public static Domain.Validation.ValueObjects.ApproverRole ToValidation(Domain.Payables.Entities.ApproverRole r) =>
        Enum.Parse<Domain.Validation.ValueObjects.ApproverRole>(r.ToString());

    public static Domain.Payables.Entities.ApproverRole ToPayables(Domain.Validation.ValueObjects.ApproverRole r) =>
        Enum.Parse<Domain.Payables.Entities.ApproverRole>(r.ToString());

    public static string ToRoleName(Domain.Validation.ValueObjects.ApproverRole r) => r.ToString();

    public static Domain.Payables.Entities.ApproverRole? PayablesRoleOf(ActorContext actor) =>
        Enum.GetValues<Domain.Payables.Entities.ApproverRole>().Cast<Domain.Payables.Entities.ApproverRole?>()
            .FirstOrDefault(r => actor.IsInRole(r!.Value.ToString()));
}
