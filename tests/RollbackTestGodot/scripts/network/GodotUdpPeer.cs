using Godot;
using PleaseUndo;
using System;
using System.Collections.Generic;

public class GodotUdpPeer : Node, IPeerNetAdapter
{
	private PacketPeerUDP _peer;

	public GodotUdpPeer(int localPort, String remoteAddress, int remotePort)
	{
		_peer = new PacketPeerUDP();
		_peer.SetDestAddress(remoteAddress, remotePort);
		_peer.Listen(localPort);
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

	public void Close()
	{
		_peer.Close();
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


