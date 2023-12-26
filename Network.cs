public class Network
{
    private Dictionary<ushort, Host> _portToHostMap = new();

    private void handlePacket(Packet p) {
        if (!_portToHostMap.TryGetValue(p.DestPort, out var host)) {
            Console.Error.WriteLine($"discarding packet to {p.DestPort}: port is not open");
            return;
        }

        host.PacketAvailable(p);
    }

    public Host Open(ushort port) {
        var h = Host.Open(port, handlePacket);
        if (!_portToHostMap.TryAdd(port, h))
            throw new Exception($"Port {port} was already open");

        return h;
    }
}