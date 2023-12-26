using System.Diagnostics;
using System.Text;

public class Host {
    [Flags]
    public enum HostOptions { None, Sniff, CombineFinAck }

    // order-sensitive!!! `PacketAvailable` depends on the ordering here!
    public enum HostState {
        Inactive,
        Listening, ClientConnecting,
        ServerConnecting,
        Established,
        Closing_Sender_WaitAck, Closing_Sender_WaitFin, Closing_Receiver_WaitAck
    }

    public ushort Port { get; private init; }
    public HostState State { get; private set; }

    public int NextSeqToSend { get; private set; } = 0;
    public int LastAckSent { get; private set; } = 0;

    public ushort OtherPort { get; private set; } = 0;

    public HostOptions Options { get; private set; }

    public bool Sniff {
        get => (Options & HostOptions.Sniff) == HostOptions.Sniff;
        set { if (value) Options |= HostOptions.Sniff; }
    }

    public bool CombineFinAck {
        get => (Options & HostOptions.CombineFinAck) == HostOptions.CombineFinAck;
        set { if (value) Options |= HostOptions.CombineFinAck; }
    }

    private Action<Packet> _sendPacket;

    private Host(Action<Packet> sendPacket) =>
        _sendPacket = p => { LogPacket(p); sendPacket(p); };

    public static Host Open(ushort port, Action<Packet> sendPacket)
        => new(sendPacket) { Port = port, State = HostState.Inactive };

    public void Listen() => State = HostState.Listening;
    internal void Listen(int seq) { Listen(); NextSeqToSend = seq; }
    public void Connect(ushort to) => Connect(to, Random.Shared.Next(0, 1024));
    public void Connect(ushort to, int seq) {
        OtherPort = to;
        NextSeqToSend = seq;
        NextSeqToSend++;
        State = HostState.ClientConnecting;
        _sendPacket(Packet.Syn(Port, to, NextSeqToSend-1, 0));
    }

    public void Send(ReadOnlyMemory<byte> data) {
        if (State != HostState.Established) {
            Warning($"cannot send while connection is not established");
            return;
        }

        var oldSeq = NextSeqToSend;
        NextSeqToSend += data.Length;
        _sendPacket(new Packet(
            Port, OtherPort,
            oldSeq,
            LastAckSent,
            5, default, 1024,
            0, 0,
            ReadOnlyMemory<int>.Empty,
            data
        ));
    }

    public void Close() {
        if (State != HostState.Established) {
            Warning($"cannot close non-established connection");
            return;
        }

        State = HostState.Closing_Sender_WaitAck;
        NextSeqToSend++;
        _sendPacket(new Packet(
            Port, OtherPort,
            NextSeqToSend-1,
            LastAckSent,
            5, TCPFlags.FIN, 1024,
            0, 0,
            ReadOnlyMemory<int>.Empty,
            ReadOnlyMemory<byte>.Empty
        ));
    }

    private void Info(string msg) => Console.Error.WriteLine($"\x1b[2m{State}({Port}) | " + msg + "\x1b[0m");
    private void Warning(string msg) => Console.Error.WriteLine($"{State}({Port}) | " + msg);

    public void PacketAvailable(Packet packet) {
        Debug.Assert(packet.DestPort == Port);

        LogPacket(packet);

        var otherPort = packet.SrcPort;

        if (State > HostState.Listening && otherPort != OtherPort) {
            Warning($"discarding packet from {otherPort}: already connected to {OtherPort}");
            return;
        }

        if (packet.HasFlag(TCPFlags.ACK) && packet.AckNum != NextSeqToSend) {
            Warning($"discarding packet from {otherPort}: ack didn't match (expected {NextSeqToSend}, got {packet.AckNum})");
            return;
        }

        if (State > HostState.ClientConnecting && packet.SeqNum != LastAckSent) {
            Warning($"discarding packet from {otherPort}: incorrect seq (expected {LastAckSent}, got {packet.SeqNum})");
            return;
        }

        switch (State) {
            case HostState.Inactive:
                Warning($"discarding packet from {otherPort}: host is inactive");
                return;
            case HostState.Listening: {
                if (!packet.IsSyn) {
                    Warning($"discarding packet from {otherPort}: expected SYN");
                    return;
                }

                LastAckSent = packet.SeqNum+1;
                if (NextSeqToSend == 0)
                    NextSeqToSend = Random.Shared.Next(0, 1024);
                NextSeqToSend++;
                OtherPort = otherPort;

                State = HostState.ServerConnecting;
                _sendPacket(Packet.SynAck(Port, otherPort, NextSeqToSend-1, LastAckSent));
                break;
            }
            case HostState.ClientConnecting: {
                if (!packet.HasFlags(TCPFlags.SYN, TCPFlags.ACK)) {
                    Warning($"discarding packet from {otherPort}: expected SYN-ACK");
                    return;
                }

                LastAckSent = packet.SeqNum+1;
                State = HostState.Established;
                _sendPacket(Packet.Ack(Port, otherPort, NextSeqToSend, LastAckSent));
                break;
            }
            case HostState.ServerConnecting: {
                if (!packet.IsAck) {
                    Warning($"discarding packet from {otherPort}: expected ACK");
                    return;
                }

                State = HostState.Established;
                break;
            }
            case HostState.Established: {
                if (packet.HasFlag(TCPFlags.FIN)) {
                    State = HostState.Closing_Receiver_WaitAck;

                    if (CombineFinAck) {
                        NextSeqToSend++;
                        LastAckSent = GetAckNumFor(packet);
                        _sendPacket(new Packet(
                            Port, otherPort,
                            NextSeqToSend - 1, LastAckSent,
                            5, TCPFlags.FIN | TCPFlags.ACK, 1024,
                            0, 0,
                            ReadOnlyMemory<int>.Empty,
                            ReadOnlyMemory<byte>.Empty
                        ));
                    } else {
                        Ack(packet);
                        NextSeqToSend++;
                        _sendPacket(new Packet(
                            Port, otherPort,
                            NextSeqToSend - 1, packet.SeqNum + 1,
                            5, TCPFlags.FIN, 1024,
                            0, 0,
                            ReadOnlyMemory<int>.Empty,
                            ReadOnlyMemory<byte>.Empty
                        ));
                    }

                    break;
                }

                if (packet.IsAck) {
                    Info($"received ack for {packet.AckNum}");
                    break;
                }

                if (packet.SeqNum != LastAckSent) {
                    Warning($"discard packet from {otherPort}: incorrect seq (expected {LastAckSent}, got {packet.SeqNum})");
                    return;
                }

                Info($"received data packet with seq={packet.SeqNum}");
                Console.WriteLine(otherPort + " said: " + Encoding.UTF8.GetString(packet.Data.Span));
                Ack(packet);
                break;
            }
            case HostState.Closing_Sender_WaitAck: {
                if (!packet.HasFlag(TCPFlags.ACK)) {
                    Warning($"discarding packet from {otherPort}: expected ACK or FIN-ACK");
                    return;
                }

                // if we get a FIN-ACK, we have to handle the ACK first, and then the FIN
                if (packet.HasFlag(TCPFlags.FIN))
                    goto case HostState.Closing_Sender_WaitFin;
                else
                    State = HostState.Closing_Sender_WaitFin;
                break;
            }
            case HostState.Closing_Sender_WaitFin: {
                if (!packet.HasFlag(TCPFlags.FIN)) {
                    Warning($"discarding packet from {otherPort}: expected FIN");
                    return;
                }

                State = HostState.Inactive;
                Ack(packet);
                break;
            }
            case HostState.Closing_Receiver_WaitAck: {
                if (!packet.IsAck) {
                    Warning($"discarding packet from {otherPort}: expected ACK");
                    return;
                }

                State = HostState.Inactive;
                break;
            }
        }
    }

    private void Reset() {
        NextSeqToSend = 0;
        LastAckSent = 0;
        OtherPort = 0;
    }

    private void LogPacket(Packet p) {
        if (!Sniff) return;

        Console.Error.WriteLine();
        Console.Error.WriteLine(p.Format().PadCenter(50));

        if (p.SrcPort == Port)
            Console.Error.WriteLine(new string('-', 49) + '>');
        else
            Console.Error.WriteLine('<' + new string('-', 49));
        Console.Error.WriteLine();
    }

    private int GetAckNumFor(Packet p) {
        var offset = p.Data.Length;
        if (offset == 0)
            offset = 1;

        return p.SeqNum + offset;
    }
    private void Ack(Packet p) {
        LastAckSent = GetAckNumFor(p);
        _sendPacket(new Packet(
            Port, p.SrcPort, NextSeqToSend, LastAckSent, 5, TCPFlags.ACK, 1024, 0, 0, ReadOnlyMemory<int>.Empty, ReadOnlyMemory<byte>.Empty)
        );
    }
}