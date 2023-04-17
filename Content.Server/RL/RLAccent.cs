using Content.Server.Speech;

[RegisterComponent]
public sealed class RLAccentComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("accent")]
    public string Accent = string.Empty;
}

public sealed class RLAccentSystem : EntitySystem
{
    [Dependency] private readonly RLSystem _rl = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RLAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, RLAccentComponent component, AccentGetEvent args)
    {
#if RL
        if (_rl.Available())
        {
            var call = RL.call("accent-" + component.Accent);
            call = RL.add(call, RL.str(args.Message));
            var ret = RL.reval(call);
            if (!RL.nil(ret))
            {
                args.Message = RL.cstr(ret);
            }
            else
            {
                Logger.ErrorS("RLAccent", $"The accent for '{component.Accent}' returned NIL");
            }
        }
#endif
    }
}
