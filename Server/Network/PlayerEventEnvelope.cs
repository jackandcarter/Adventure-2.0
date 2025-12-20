namespace Adventure.Server.Network
{
    public record PlayerEventEnvelope(string SessionId, string? RequestId, IMessageSender Sender, object Payload);
}
