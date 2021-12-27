using Godot;
using PleaseUndo;
using System;
using System.Collections.Generic;

public class UdpPeer<InputType> : IPeerNetAdapter<InputType>
{
    private PacketPeerUDP _peer;
    private List<NetMsg> _last_messages;

    public Error Init(int localPort, String remoteAddress, int remotePort)
    {
        _peer = new PacketPeerUDP();
        _last_messages = new List<NetMsg>();

        var destErr = _peer.SetDestAddress(remoteAddress, remotePort);
        var listenErr = _peer.Listen(localPort);

        if (destErr != Error.Ok) return destErr;
        if (listenErr != Error.Ok) return listenErr;

        return Error.Ok;
    }


    private void SendMsg(byte[] msg)
    {
        _peer.PutPacket(msg);
    }

    // Show be called every iteration
    public void Poll()
    {
        if (_peer.IsListening())
        {
            if (_peer.GetAvailablePacketCount() > 0)
            {
                for (int i = 0; i < _peer.GetAvailablePacketCount(); i++)
                {
                    var msg = _peer.GetPacket();
                    _last_messages.Add(NetMsg.Deserialize<NetMsg>(msg));
                }
            }
        }
    }

    public override void Send(NetMsg msg)
    {
        SendMsg(NetMsg.Serialize(msg));
    }

    public override List<NetMsg> ReceiveAllMessages()
    {
        var temp = new List<NetMsg>(_last_messages);
        _last_messages.Clear();
        return temp;
    }
}


