public abstract record Host(
    short Port
) {
    public record Listening(short Port) : Host(Port);
    public record Connecting(short ClientPort, short ServerPort, int StartSeq) : Host(ClientPort);
    public record Connected(short ClientPort, short ServerPort, int ClientSeq, int LastAck) : Host(ClientPort);
}