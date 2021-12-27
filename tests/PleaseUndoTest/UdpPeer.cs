using PleaseUndo;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

class UdpPeer : IPeerNetAdapter
{
    private UdpClient _peer;
    private IPEndPoint _remoteEndPoint;
    private List<NetMsg> _receivedMessages = new List<NetMsg>();

    public UdpPeer(int localPort, string remoteAddress, int remotePort)
    {
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
        _peer = new UdpClient(localPort);
        _peer.Connect(_remoteEndPoint);
    }

    public void SendMsg(byte[] msg)
    {
        _peer.Send(msg, msg.Length);
    }

    public void Poll()
    {
        while (_peer.Available > 0)
        {
            var msg = _peer.Receive(ref _remoteEndPoint);
            _receivedMessages.Add(NetMsg.Deserialize<NetMsg>(msg));
        }
    }

    public void Close()
    {
        _peer.Close();
    }

    public override List<NetMsg> ReceiveAllMessages()
    {
        var messagesCopy = new List<NetMsg>(_receivedMessages);
        _receivedMessages.Clear();
        return messagesCopy;
    }

    public override void Send(NetMsg msg)
    {
        SendMsg(NetMsg.Serialize(msg));
    }
}
