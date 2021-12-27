using Godot;
using PleaseUndo;
using System;
using System.Collections.Generic;

public class GodotUdpPeer : IPeerNetAdapter
{
    private PacketPeerUDP _peer;

    public Error Init(int localPort, String remoteAddress, int remotePort)
    {
        _peer = new PacketPeerUDP();

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

    public List<NetMsg> Poll()
    {
        var messages = new List<NetMsg>();
        if (_peer.IsListening())
        {
            if (_peer.GetAvailablePacketCount() > 0)
            {
                for (int i = 0; i < _peer.GetAvailablePacketCount(); i++)
                {
                    var msg = _peer.GetPacket();
                    messages.Add(NetMsg.Deserialize<NetMsg>(msg));
                }
            }
        }
        return messages;
    }

    public override void Send(NetMsg msg)
    {
        SendMsg(NetMsg.Serialize(msg));
    }

    public override List<NetMsg> ReceiveAllMessages()
    {
        return Poll();
    }
}


