using System.Text;

[Flags]
public enum TCPFlags : byte {
    URG = 0b0010_0000,
    ACK = 0b0001_0000,
    PSH = 0b0000_1000,
    RST = 0b0000_0100,
    SYN = 0b0000_0010,
    FIN = 0b0000_0001
}

public readonly record struct Packet(
    ushort SrcPort, ushort DestPort,
    int SeqNum,
    int AckNum,
    byte HeaderSize /* in words */, TCPFlags Flags, ushort WindowSize,
    ushort Checksum, ushort UrgentPointer,
    ReadOnlyMemory<int> Options,
    ReadOnlyMemory<byte> Data
) {
    public bool IsSyn => Flags == TCPFlags.SYN;
    public bool IsAck => Flags == TCPFlags.ACK;

    public static Packet Syn(ushort from, ushort to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.SYN, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);
    public static Packet Ack(ushort from, ushort to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.ACK, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);
    public static Packet SynAck(ushort from, ushort to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.SYN | TCPFlags.ACK, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);

    public string Format(bool showWindowSize = false) {
        var flags = GetFlags();

        // if none of the flags are set, then this is a data packet
        if (flags.Length == 0)
            return $"DATA({Data.Length}) seq={SeqNum}";

        var sb = new StringBuilder();

        sb.AppendJoin('-', flags.ToArray());

        if (HasFlag(TCPFlags.SYN) | HasFlag(TCPFlags.FIN))
            sb.Append(" seq=").Append(SeqNum);
        if (HasFlag(TCPFlags.ACK))
            sb.Append(" ack=").Append(AckNum);
        if (showWindowSize)
            sb.Append(" win=").Append(WindowSize);

        return sb.ToString();
    }

    Span<TCPFlags> GetFlags() {
        var setFlags = new TCPFlags[8];
        var setCount = 0;

        for (byte i = 1; i < (byte)TCPFlags.URG; i <<= 1) {
            if (((byte)Flags & i) != 0)
                setFlags[setCount++] = (TCPFlags)i;
        }

        return setFlags.AsSpan(0, setCount);
    }

    public bool HasFlag(TCPFlags f0) => (Flags & f0) != 0;
    public bool HasFlags(TCPFlags f0, TCPFlags f1) => (Flags & (f0 | f1)) == (f0 | f1);
    public bool HasFlags(TCPFlags f0, TCPFlags f1, TCPFlags f2) => (Flags & (f0 | f1 | f2)) == (f0 | f1 | f2);
    public bool HasFlags(TCPFlags f0, TCPFlags f1, TCPFlags f2, TCPFlags f3) => (Flags & (f0 | f1 | f2 | f3)) == (f0 | f1 | f2 | f3);
    public bool HasFlags(TCPFlags f0, TCPFlags f1, TCPFlags f2, TCPFlags f3, TCPFlags f4) => (Flags & (f0 | f1 | f2 | f3 | f4)) == (f0 | f1 | f2 | f3 | f4);
    public bool HasFlags(TCPFlags f0, TCPFlags f1, TCPFlags f2, TCPFlags f3, TCPFlags f4, TCPFlags f5) => (Flags & (f0 | f1 | f2 | f3 | f4 | f5)) == (f0 | f1 | f2 | f3 | f4 | f5);
}