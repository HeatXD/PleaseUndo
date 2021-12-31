using Godot;
using PleaseUndo;
using System;
using System.Collections.Generic;

public class GodotUdpPeer : PacketPeerUDP, IPeerNetAdapter
{
	public GodotUdpPeer(int localPort, String remoteAddress, int remotePort)
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
			if (GetAvailablePacketCount() > 0)
			{
				for (int i = 0; i < GetAvailablePacketCount(); i++)
				{
					var msg = GetPacket();
					messages.Add(NetMsg.Deserialize<NetMsg>(msg));
				}
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


