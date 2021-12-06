using PleaseUndo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PleaseUndoTest
{
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
