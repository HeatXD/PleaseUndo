using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace PleaseUndo
{
    public class UdpPeerNetAdapter : IPeerNetAdapter
    {
        const int SIO_UDP_CONNRESET = -1744830452;

        private UdpClient _peer;
        private IPEndPoint _remoteEndPoint;

        public UdpPeerNetAdapter(int localPort, string remoteAddress, int remotePort)
        {
            Logger.Log("UdpPeer({0})", new { localPort, remoteAddress, remotePort });
            _peer = new UdpClient(localPort);
            _peer.Connect(remoteAddress, remotePort);
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);

            // Ignore the connect reset message in Windows to prevent a UDP shutdown exception
            // As seen in https://github.com/dhavatar/ggpo-sharp/blob/master/src/Network/Udp.cs
            _peer.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
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

        private void SendMsg(byte[] msg)
        {
            _peer.Send(msg, msg.Length);
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
}
