namespace Content.Server.MachineLinking.Events
{
    public enum SignalState
    {
        Momentary, // Instantaneous pulse high, compatibility behavior
        Low,
        High
    }

    public sealed class SignalReceivedEvent : EntityEventArgs
    {
        public readonly string Port;
        public readonly SignalState State;

        public SignalReceivedEvent(string port, SignalState state)
        {
            Port = port;
            State = state;
        }
    }
}
