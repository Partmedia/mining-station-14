using System.Linq;
using Content.Server.Administration;
using Robust.Shared.Console;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Content.Shared.Administration;
using Content.Server.StationEvents;

namespace Content.Server.StationEvents.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    sealed class RunEventCommand : IConsoleCommand
    {
        [Dependency] private readonly IPrototypeManager _prototype = default!;

        public string Command => "runevent";
        public string Description => "Runs a specified station event";
        public string Help => $"Usage: {Command} <event id>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!_prototype.TryIndex<GameRulePrototype>(args[0], out var proto))
            {
                shell.WriteError(Loc.GetString("invalid event id"));
                return ;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();
            var eventManagerSystem = entityManager.System<EventManagerSystem>();

            eventManagerSystem.GameTicker.AddGameRule(proto);
            shell.WriteError(Loc.GetString("station-event-system-run-event", ("eventName", args[0])));
        }
    }
}
