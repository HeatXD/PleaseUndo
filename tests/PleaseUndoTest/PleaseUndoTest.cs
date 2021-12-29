using PleaseUndo;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PleaseUndoTest
{
    [TestClass]
    public class P2PTest
    {
        const int LOCAL_PORT_1 = 7005;
        const int LOCAL_PORT_2 = 7006;
        const string LOCAL_ADDRESS = "127.0.0.1";


        [TestMethod]
        public void Test_P2P()
        {
            var session1_adapter = new UdpPeerNetAdapter(LOCAL_PORT_1, LOCAL_ADDRESS, LOCAL_PORT_2);
            var session2_adapter = new UdpPeerNetAdapter(LOCAL_PORT_2, LOCAL_ADDRESS, LOCAL_PORT_1);

            try
            {
                var cbs1 = new GGPOSessionCallbacks
                {
                    OnEvent = (GGPOEvent ev) => { return false; },
                    OnBeginGame = () => { return false; },
                    OnAdvanceFrame = () => { return false; },
                    OnLoadGameState = (byte[] buffer, int len) => { return false; },
                    OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) => { return false; },
                };
                var session1 = new Peer2PeerBackend(ref cbs1, 2, sizeof(int));
                var session1_handle1 = new GGPOPlayerHandle { };
                var session1_handle2 = new GGPOPlayerHandle { };

                session1.AddLocalPlayer(new GGPOPlayer { player_num = 1 }, ref session1_handle1);
                session1.AddRemotePlayer(new GGPOPlayer { player_num = 2 }, ref session1_handle2, session1_adapter);

                var cbs2 = new GGPOSessionCallbacks
                {
                    OnEvent = (ev) => { return false; },
                    OnBeginGame = () => { return false; },
                    OnAdvanceFrame = () => { return false; },
                    OnLoadGameState = (byte[] buffer, int len) => { return false; },
                    OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) => { return false; },
                };
                var session2 = new Peer2PeerBackend(ref cbs2, 2, sizeof(int));
                var session2_handle1 = new GGPOPlayerHandle { };
                var session2_handle2 = new GGPOPlayerHandle { };

                session2.AddRemotePlayer(new GGPOPlayer { player_num = 1 }, ref session2_handle1, session2_adapter);
                session2.AddLocalPlayer(new GGPOPlayer { player_num = 2 }, ref session2_handle2);

                for (var i = 0; i < 100; i++)
                {
                    session1.DoPoll(100);
                    session2.DoPoll(100);
                }
            }
            finally
            {
                session1_adapter.Close();
                session2_adapter.Close();
            }

            // throw new System.Exception(); // uncomment if you want logs...
        }

        [TestMethod]
        public void Test_UDP()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            var client1 = new UdpClient(LOCAL_PORT_1);
            var client2 = new UdpClient(LOCAL_PORT_2);

            client1.Connect(LOCAL_ADDRESS, LOCAL_PORT_2);
            client2.Connect(LOCAL_ADDRESS, LOCAL_PORT_1);

            client1.Send(new byte[] { 1, 2, 3 }, 3);
            client2.Send(new byte[] { 4, 5, 6, 7, 8 }, 5);

            Assert.AreEqual(5, client1.Available);
            Assert.AreEqual(3, client2.Available);
            Assert.AreEqual(5, client1.Receive(ref ep).Length);
            Assert.AreEqual(3, client2.Receive(ref ep).Length);

            client1.Send(new byte[] { 7 }, 1);
            client2.Send(new byte[] { 0, 0 }, 2);

            Assert.AreEqual(2, client1.Available);
            Assert.AreEqual(1, client2.Available, 1);
            Assert.AreEqual(2, client1.Receive(ref ep).Length);
            Assert.AreEqual(1, client2.Receive(ref ep).Length);

            var packet1 = NetMsg.Serialize(new NetSyncRequestMsg { random_request = 32 });
            client1.Send(packet1, packet1.Length);
            Assert.AreEqual((uint)32, NetMsg.Deserialize<NetSyncRequestMsg>(client2.Receive(ref ep)).random_request);

            var packet2 = NetMsg.Serialize(new NetSyncRequestMsg { random_request = 44 });
            client2.Send(packet2, packet2.Length);
            Assert.AreEqual((uint)44, NetMsg.Deserialize<NetSyncRequestMsg>(client1.Receive(ref ep)).random_request);

            client1.Close();
            client2.Close();
        }
    }
    [TestClass]
    public class NetMsgSerializationTest
    {
        [TestMethod]
        public void Test_NetMsgSerialization()
        {
            var inputMessage = new NetInputMsg { };
            var inputAckMessage = new NetInputAckMsg { };

            // inputAckMessage should be smaller than inputMessage.
            Assert.IsTrue(NetMsg.Serialize(inputAckMessage).Length < NetMsg.Serialize(inputMessage).Length);
            // two messages serialized with the same data should have the same length.
            Assert.IsTrue(NetMsg.Serialize(inputAckMessage).Length == NetMsg.Serialize(inputAckMessage).Length);
        }

        [TestMethod]
        public void Test_NetInputMsgSerialization()
        {
            var inputMessage = new NetInputMsg
            {
                ack_frame = 55,
                start_frame = 32,
                sequence_number = 12,
                peer_connect_status = new NetMsg.ConnectStatus[2]
            };
            var inputMessageData = NetMsg.Serialize(inputMessage);
            var inputMessageCopy = NetMsg.Deserialize<NetInputMsg>(inputMessageData);

            Assert.AreEqual(inputMessage.ack_frame, inputMessageCopy.ack_frame);
            Assert.AreEqual(inputMessage.start_frame, inputMessageCopy.start_frame);
            Assert.AreEqual(inputMessage.sequence_number, inputMessageCopy.sequence_number);
            Assert.AreEqual(inputMessage.peer_connect_status.Length, inputMessageCopy.peer_connect_status.Length);
        }
    }
}
