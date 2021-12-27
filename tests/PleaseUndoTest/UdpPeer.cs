using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using PleaseUndo;

class UdpPeer : IPeerNetAdapter
{
    private UdpClient _peer;
    private IPEndPoint _remoteEndPoint;

    public void Init(int localPort, string remoteAddress, int remotePort)
    {
        try
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
            _peer = new UdpClient(localPort);
            _peer.Connect(_remoteEndPoint);
        }
        catch (System.Exception e)
        {
            throw e;
        }
    }

    public void SendMsg(byte[] msg)
    {
        _peer.Send(msg, msg.Length);
    }

    public List<NetMsg> Poll()
    {
        var messages = new List<NetMsg>();
        while (_peer.Available > 0)
        {
            var msg = _peer.Receive(ref _remoteEndPoint);
            messages.Add(NetMsg.Deserialize<NetMsg>(msg));
        }
        return messages;
    }

    public void Close()
    {
        _peer.Close();
    }

    public override List<NetMsg> ReceiveAllMessages()
    {
        return Poll();
    }

    public override void Send(NetMsg msg)
    {
        SendMsg(NetMsg.Serialize(msg));
    }
}
