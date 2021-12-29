using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace PleaseUndo
{
    public class UdpPeerNetAdapter : IPeerNetAdapter
    {
        private UdpClient _peer;
        private IPEndPoint _remoteEndPoint;

        public UdpPeerNetAdapter(int localPort, string remoteAddress, int remotePort)
        {
            Logger.Log("UdpPeer({0})", new { localPort, remoteAddress, remotePort });
            _peer = new UdpClient(localPort);
            _peer.Connect(remoteAddress, remotePort);
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
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

        public override void Send(NetMsg msg)
        {
            SendMsg(NetMsg.Serialize(msg));
        }

        public override List<NetMsg> ReceiveAllMessages()
        {
            return Poll();
        }

        private void SendMsg(byte[] msg)
        {
            _peer.Send(msg, msg.Length);
        }
    }
}
