using Godot;
using PleaseUndo;
using System;
using System.Collections.Generic;

public class GodotPeerUDP : PacketPeerUDP, IPeerNetAdapter
{
    public GodotPeerUDP(int localPort, String remoteAddress, int remotePort)
    {
        SetDestAddress(remoteAddress, remotePort);
        Listen(localPort);
    }

    private void SendMsg(byte[] msg)
    {
        PutPacket(msg);
    }

    public List<NetMsg> Poll()
    {
        var messages = new List<NetMsg>();
        if (IsListening())
        {
            for (int i = 0; i < GetAvailablePacketCount(); i++)
            {
                var msg = GetPacket();
                messages.Add(NetMsg.Deserialize<NetMsg>(msg));
            }
        }
        return messages;
    }

    public void Send(NetMsg msg)
    {
        SendMsg(NetMsg.Serialize(msg));
    }

    public List<NetMsg> ReceiveAllMessages()
    {
        return Poll();
    }
}


