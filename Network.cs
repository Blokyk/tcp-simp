public class Network
{
    private Dictionary<short, LinkedList<Packet>> _packets = new();

    public IEnumerable<short> OpenPorts => _packets.Keys;

    public void Open(short port) {
        if (!_packets.TryAdd(port, []))
            throw new Exception($"Port {port} was already open");
    }

    public Packet? ConsumeMatchingPacket(short port, Func<Packet, bool> shouldConsume) {
        var packets = GetPacketsForPort(port);

        foreach (var p in packets) {
            if (shouldConsume(p)) {
                packets.Remove(p);
                return p;
            }
        }

        return null;
    }

    private LinkedList<Packet> GetPacketsForPort(short port) {
        if (!_packets.TryGetValue(port, out var packetList))
            throw new Exception($"Port {port} is not open");

        return packetList;
    }

    public IEnumerable<Packet> PeekPacketsForPort(short port)
        => GetPacketsForPort(port);

    public void PushPacket(Packet packet)
        => GetPacketsForPort(packet.DestPort).AddFirst(packet);
}