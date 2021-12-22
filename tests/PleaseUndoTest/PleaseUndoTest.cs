using PleaseUndo;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class DummyAdapter : PleaseUndo.IPeerNetAdapter<int>
{
    public override void Send(NetMsg msg) { }
    public override List<NetMsg> ReceiveAllMessages() { return null; }
}

namespace PleaseUndoTest
{
    [TestClass]
    public class P2PTest
    {
        [TestMethod]
        public void TestP2P()
        {
            var cbs1 = new GGPOSessionCallbacks
            {
                OnEvent = (GGPOEvent ev) => { return false; },
                OnBeginGame = () => { return false; },
                OnAdvanceFrame = () => { return false; },
                OnLoadGameState = (byte[] buffer, int len) => { return false; },
                OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) => { return false; },
            };
            var session1 = new Peer2PeerBackend<int>(ref cbs1, 2);
            var session1_adapter = new DummyAdapter();
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
            var session2 = new Peer2PeerBackend<int>(ref cbs2, 2);
            var session2_adapter = new DummyAdapter();
            var session2_handle1 = new GGPOPlayerHandle { };
            var session2_handle2 = new GGPOPlayerHandle { };

            session1.AddRemotePlayer(new GGPOPlayer { player_num = 1 }, ref session1_handle1, session2_adapter);
            session1.AddLocalPlayer(new GGPOPlayer { player_num = 2 }, ref session1_handle2);
        }
    }
    [TestClass]
    public class NetMsgSerializationTest
    {
        [TestMethod]
        public void NetMsgSerialization()
        {
            var inputMessage = new NetInputMsg { };
            var inputAckMessage = new NetInputAckMsg { };

            // inputAckMessage should be smaller than inputMessage.
            Assert.IsTrue(NetMsg.Serialize(inputAckMessage).Length < NetMsg.Serialize(inputMessage).Length);
            // two messages serialized with the same data should have the same length.
            Assert.IsTrue(NetMsg.Serialize(inputAckMessage).Length == NetMsg.Serialize(inputAckMessage).Length);
        }

        [TestMethod]
        public void NetInputMsgSerialization()
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
