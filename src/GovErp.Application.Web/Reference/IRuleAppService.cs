using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Reference.Commands;
using GovErp.Application.Web.Reference.Contracts;

namespace GovErp.Application.Web.Reference;

public interface IRuleAppService
{
    Task<CommandResult<RuleVm>> CreateVersionAsync(NewRuleVersionCommand cmd, ActorContext actor, CancellationToken ct = default);
}
