using Grpc.Net.Client;

using Content.Server.RL;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

public class RLSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private GrpcChannel? Channel;

    public override void Initialize()
    {
        var endpoint = _cfg.GetCVar(CCVars.RLEndpoint);
        if (endpoint == string.Empty)
            return;

        Channel = GrpcChannel.ForAddress(endpoint);
    }

    public bool Available()
    {
        return Channel is not null;
    }

    public RL.RLClient Client()
    {
        return new RL.RLClient(Channel);
    }

    public DateTime Deadline()
    {
        return DateTime.UtcNow.AddSeconds(_cfg.GetCVar(CCVars.RLTimeout));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (Channel is not null)
            Channel.Dispose();
    }
}
