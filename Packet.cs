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
    short SrcPort, short DestPort,
    int SeqNum,
    int AckNum,
    byte HeaderSize /* in words */, TCPFlags Flags, short WindowSize,
    short Checksum, short UrgentPointer,
    ReadOnlyMemory<int> Options,
    ReadOnlyMemory<byte> Data
) {
    public bool IsSyn => (Flags & TCPFlags.SYN) == 0;
    public bool IsAck => (Flags & TCPFlags.ACK) == 0;

    public static Packet Syn(short from, short to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.SYN, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);
    public static Packet Ack(short from, short to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.ACK, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);
    public static Packet SynAck(short from, short to, int seq, int ack)
        => new(from, to, seq, ack, 5, TCPFlags.SYN | TCPFlags.ACK, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty);

    public string Format(bool showWindowSize) {
        var flags = GetFlags();

        // if none of the flags are set, then this is a data packet
        if (flags.Length == 0)
            return $"DATA({Data.Length}) seq={SeqNum}";

        var sb = new StringBuilder();

        sb.AppendJoin('-', flags);

        if (IsSyn)
            sb.Append(" seq=").Append(SeqNum);
        if (IsAck)
            sb.Append(" ack=").Append(AckNum);
        if (showWindowSize)
            sb.Append(" win=").Append(WindowSize);

        return sb.ToString();
    }

    TCPFlags[] GetFlags() {
        TCPFlags[] setFlags = new TCPFlags[8];
        var setCount = 0;

        for (byte i = 0; i < (byte)TCPFlags.URG; i <<= 1) {
            if (((byte)Flags & i) != 0)
                setFlags[setCount++] = (TCPFlags)i;
        }

        return setFlags;
    }

}