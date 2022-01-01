using PleaseUndo;
using System.Net;
using System.Linq;
using MessagePack;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PleaseUndoTest
{
    [TestClass]
    public class SessionTest
    {
        [MessagePackObject]
        public struct GameState
        {
            [KeyAttribute(0)]
            public int Count;
        }

        const int INPUT_SIZE = 8;
        const int LOCAL_PORT_1 = 7005;
        const int LOCAL_PORT_2 = 7006;
        const string LOCAL_ADDRESS = "127.0.0.1";
        static byte[] RANDOM_INPUTS = new byte[32] { 9, 83, 206, 251, 56, 198, 11, 125, 212, 220, 127, 160, 240, 131, 148, 134, 157, 18, 11, 192, 232, 197, 255, 181, 15, 138, 197, 109, 242, 181, 194, 75 };

        private byte GetSeededInput(ref int seed)
        {
            seed += 1;
            return RANDOM_INPUTS[seed % RANDOM_INPUTS.Length];
        }

        [TestMethod]
        public void Test_P2P()
        {
            var session1_adapter = new UdpPeerNetAdapter(LOCAL_PORT_1, LOCAL_ADDRESS, LOCAL_PORT_2);
            var session2_adapter = new UdpPeerNetAdapter(LOCAL_PORT_2, LOCAL_ADDRESS, LOCAL_PORT_1);

            try
            {
                var session1_synchronized = false;
                var cbs1 = new PUSessionCallbacks
                {
                    OnEvent = (PUEvent ev) =>
                    {
                        if (ev.code == PUEventCode.PU_EVENTCODE_SYNCHRONIZED_WITH_PEER)
                        {
                            session1_synchronized = true;
                            return true;
                        }
                        return false;
                    },
                    OnBeginGame = () => { return false; },
                    OnAdvanceFrame = () => { return false; },
                    OnLoadGameState = (byte[] buffer, int len) => { return false; },
                    OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) => { return false; },
                };
                var session1 = new Peer2PeerBackend(ref cbs1, 2, INPUT_SIZE);
                var session1_handle1 = new PUPlayerHandle { };
                var session1_handle2 = new PUPlayerHandle { };

                Assert.AreEqual(PUErrorCode.PU_OK, session1.AddLocalPlayer(new PUPlayer { player_num = 1 }, ref session1_handle1));
                Assert.AreEqual(PUErrorCode.PU_OK, session1.AddRemotePlayer(new PUPlayer { player_num = 2 }, ref session1_handle2, session1_adapter));

                var session2_synchronized = false;
                var cbs2 = new PUSessionCallbacks
                {
                    OnEvent = (ev) =>
                    {
                        if (ev.code == PUEventCode.PU_EVENTCODE_SYNCHRONIZED_WITH_PEER)
                        {
                            session2_synchronized = true;
                            return true;
                        }
                        return false;
                    },
                    OnBeginGame = () => { return false; },
                    OnAdvanceFrame = () => { return false; },
                    OnLoadGameState = (byte[] buffer, int len) => { return false; },
                    OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) => { return false; },
                };
                var session2 = new Peer2PeerBackend(ref cbs2, 2, INPUT_SIZE);
                var session2_handle1 = new PUPlayerHandle { };
                var session2_handle2 = new PUPlayerHandle { };

                Assert.AreEqual(PUErrorCode.PU_OK, session2.AddRemotePlayer(new PUPlayer { player_num = 1 }, ref session2_handle1, session2_adapter));
                Assert.AreEqual(PUErrorCode.PU_OK, session2.AddLocalPlayer(new PUPlayer { player_num = 2 }, ref session2_handle2));

                for (var i = 0; i < 10; i++)
                {
                    Assert.AreEqual(PUErrorCode.PU_OK, session1.DoPoll(100));
                    Assert.AreEqual(PUErrorCode.PU_OK, session2.DoPoll(100));
                    if (session1_synchronized && session2_synchronized) { break; }
                }

                Assert.AreEqual(true, session1_synchronized);
                Assert.AreEqual(true, session2_synchronized);

                var values = new byte[16];
                var disconnect_flags = 0;

                Assert.AreEqual(PUErrorCode.PU_OK, session1.AddLocalInput(session1_handle1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, INPUT_SIZE));
                Assert.AreEqual(PUErrorCode.PU_OK, session2.AddLocalInput(session2_handle2, new byte[] { 9, 1, 2, 3, 4, 5, 6, 7 }, INPUT_SIZE));

                Assert.AreEqual(PUErrorCode.PU_OK, session1.SyncInput(ref values, INPUT_SIZE, ref disconnect_flags));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, values.Take(8).ToArray());
                CollectionAssert.AreNotEqual(new byte[] { 9, 1, 2, 3, 4, 5, 6, 7 }, values.Skip(8).Take(8).ToArray());
                Assert.AreEqual(PUErrorCode.PU_OK, session2.SyncInput(ref values, INPUT_SIZE, ref disconnect_flags));
                CollectionAssert.AreEqual(new byte[] { 9, 1, 2, 3, 4, 5, 6, 7 }, values.Skip(8).Take(8).ToArray());
                CollectionAssert.AreNotEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, values.Take(8).ToArray());

                Assert.AreEqual(PUErrorCode.PU_OK, session1.DoPoll(100));
                Assert.AreEqual(PUErrorCode.PU_OK, session2.DoPoll(100));

                Assert.AreEqual(PUErrorCode.PU_OK, session1.SyncInput(ref values, INPUT_SIZE, ref disconnect_flags));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7 }, values);
                Assert.AreEqual(PUErrorCode.PU_OK, session2.SyncInput(ref values, INPUT_SIZE, ref disconnect_flags));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7 }, values);

                Assert.AreEqual(PUErrorCode.PU_OK, session1.IncrementFrame());
                Assert.AreEqual(PUErrorCode.PU_OK, session2.IncrementFrame());
            }
            finally
            {
                session1_adapter.Close();
                session2_adapter.Close();
            }

            // throw new System.Exception(); // uncomment if you want logs...
        }

        [TestMethod]
        public void Test_Synctest()
        {
            var seed = 0;
            var state = new GameState { };
            var localHandle = new PUPlayerHandle { };
            var remoteHandle = new PUPlayerHandle { };
            PUSession synctest = null;

            var cb = new PUSessionCallbacks
            {
                OnEvent = (PUEvent ev) => { return false; },
                OnBeginGame = () => { return false; },
                OnAdvanceFrame = () =>
                {
                    var values = new byte[16];
                    var disconnectFlags = 0;

                    Assert.AreEqual(PUErrorCode.PU_OK, synctest.SyncInput(ref values, INPUT_SIZE, ref disconnectFlags));

                    for (var j = 0; j < values.Length; j++)
                    {
                        state.Count += values[j];
                    }

                    Assert.AreEqual(PUErrorCode.PU_OK, synctest.IncrementFrame());
                    return true;
                },
                OnLoadGameState = (byte[] buffer, int len) =>
                {
                    state = MessagePackSerializer.Deserialize<GameState>(buffer);
                    return true;
                },
                OnSaveGameState = (ref byte[] buffer, ref int len, ref int checksum, int frame) =>
                {
                    buffer = MessagePackSerializer.Serialize(state);
                    len = buffer.Length;
                    checksum = state.Count;
                    return true;
                },
            };

            synctest = new SyncTestBackend(ref cb, 3, 2, INPUT_SIZE);

            Assert.AreEqual(PUErrorCode.PU_OK, synctest.AddLocalPlayer(new PUPlayer { player_num = 2 }, ref localHandle));
            Assert.AreEqual(PUErrorCode.PU_OK, synctest.AddRemotePlayer(new PUPlayer { player_num = 1 }, ref remoteHandle, null));

            Assert.AreEqual(PUErrorCode.PU_OK, synctest.DoPoll(100));

            var values = new byte[16];
            var disconnectFlags = 0;

            for (var i = 0; i < 100; i++)
            {
                Assert.AreEqual(PUErrorCode.PU_OK, synctest.AddLocalInput(localHandle, new byte[] { GetSeededInput(ref seed), 2, 3, 4, 5, 6, 7, 8 }, INPUT_SIZE));
                Assert.AreEqual(PUErrorCode.PU_OK, synctest.SyncInput(ref values, INPUT_SIZE, ref disconnectFlags));

                for (var j = 0; j < values.Length; j++)
                {
                    state.Count += values[j];
                }

                Assert.AreEqual(PUErrorCode.PU_OK, synctest.IncrementFrame());
            }
        }
    }

    [TestClass]
    public class NetworkTest
    {
        const int LOCAL_PORT_1 = 7007;
        const int LOCAL_PORT_2 = 7008;
        const string LOCAL_ADDRESS = "127.0.0.1";

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
