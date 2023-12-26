public class TCPNetwork
{
    private Network _network = new();
    private Dictionary<short, Host> _hosts = new();

    public void Listen(short port) {
        _network.Open(port);
        _hosts.Add(port, new Host.Listening(port));
    }

    public void Connect(short from, short to) {
        _network.Open(from);

        var startSeq = Random.Shared.Next();
        _network.PushPacket(Packet.Syn(from, to, startSeq, 0));

        _hosts.Add(from, new Host.Connecting(from, to, startSeq));
    }

    public void Accept(short port) {
        if (!_hosts.TryGetValue(port, out var server))
            throw new Exception($"Port {port} was not open");
        if (server is not Host.Listening)
            throw new Exception($"Port {port} is not listening");

        var nullablePacket = _network.ConsumeMatchingPacket(port, p => p.IsSyn);
        if (!nullablePacket.HasValue)
            throw new Exception($"Port {port} doesn't have any client to accept");

        var packet = nullablePacket.Value;

        var clientPort = packet.SrcPort;
        var clientSeq = packet.SeqNum;

        var startSeq = Random.Shared.Next();
        _network.PushPacket(Packet.SynAck(port, clientPort, startSeq, clientSeq+1));
        _hosts[port] = new Host.Connected(port, clientPort, startSeq, clientSeq+1);
    }

    public void FinalizeConnection(short port) {
        if (!_hosts.TryGetValue(port, out var client))
            throw new Exception($"Port {port} was not open");
        if (client is not Host.Connecting)
            throw new Exception($"Port {port} is not trying to connect");
    }
}