using System;
using System.Runtime.CompilerServices;

namespace PleaseUndo
{
    public class SyncTestBackend : PUSession
    {
        public struct SavedInfo
        {
            public int frame;
            public int checksum;
            public byte[] buf;
            public int cbuf;
            public GameInput input;
        };

        PUSessionCallbacks _callbacks;
        Sync _sync;
        int _num_players;
        int _check_distance;
        int _last_verified;
        bool _rollingback;
        bool _running;

        GameInput _last_input;
        GameInput _current_input;
        RingBuffer<SavedInfo> _saved_frames = new RingBuffer<SavedInfo>(32);

        public SyncTestBackend(ref PUSessionCallbacks cb, int frames, int num_players, int input_size)
        {
            _callbacks = cb;
            _num_players = num_players;
            _check_distance = frames;
            _last_verified = 0;
            _rollingback = false;
            _running = false;
            _current_input = new GameInput(0, null, (uint)input_size); // struct was default constructed and bits would be null, important note is that the fist parameter is not NullFrame, but default int value in C++, which is zero.
            _current_input.Erase(); // CHECKME: this is useless in C# since byte[] are zeroed

            /*
            * Initialize the synchronziation layer
            */
            NetMsg.ConnectStatus[] connect_status = null;
            _sync = new Sync(ref connect_status);
            _sync.Init(new Sync.Config
            {
                callbacks = _callbacks,
                num_prediction_frames = Sync.MAX_PREDICTION_FRAMES
            });

            /*
            * Preload the ROM
            */
            _callbacks.OnBeginGame();
        }

        public override PUErrorCode DoPoll(int timeout)
        {
            if (!_running)
            {
                _callbacks.OnEvent(new PURunningEvent { code = PUEventCode.PU_EVENTCODE_RUNNING });
                _running = true;
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode AddLocalPlayer(PUPlayer player, ref PUPlayerHandle handle)
        {
            if (player.player_num < 1 || player.player_num > _num_players)
            {
                return PUErrorCode.PU_ERRORCODE_PLAYER_OUT_OF_RANGE;
            }
            handle = new PUPlayerHandle { handle = player.player_num - 1 };
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode AddRemotePlayer(PUPlayer player, ref PUPlayerHandle handle, IPeerNetAdapter peerNetAdapter)
        {
            return AddLocalPlayer(player, ref handle);
        }

        public override PUErrorCode AddLocalInput(PUPlayerHandle player, byte[] values, int size)
        {
            if (!_running)
            {
                return PUErrorCode.PU_ERRORCODE_NOT_SYNCHRONIZED;
            }

            int index = player.handle;
            for (int i = 0; i < size; i++)
            {
                _current_input.bits[(index * size) + i] |= values[i];
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode SyncInput(ref byte[] values, int size, ref int disconnect_flags)
        {
            if (_rollingback)
            {
                _last_input = _saved_frames.Front().input;
            }
            else
            {
                if (_sync.GetFrameCount() == 0)
                {
                    _sync.SaveCurrentFrame();
                }
                _last_input = new GameInput(_current_input.frame, _current_input.bits, (uint)_current_input.bits.Length);
            }

            //values = _last_input.bits; // CHECKME: memcpy(values, _last_input.bits, size);
            Array.Copy(_last_input.bits, values, _last_input.bits.Length);

            if (disconnect_flags != 0)
            {
                disconnect_flags = 0;
            }
            return PUErrorCode.PU_OK;
        }

        public override PUErrorCode IncrementFrame()
        {
            _sync.IncrementFrame();
            _current_input.Erase();

            Logger.Log("End of frame({0})...\n", _sync.GetFrameCount());

            if (_rollingback)
            {
                return PUErrorCode.PU_OK;
            }

            int frame = _sync.GetFrameCount();
            // Hold onto the current frame in our queue of saved states.  We'll need
            // the checksum later to verify that our replay of the same frame got the
            // same results.
            SavedInfo info;
            info.frame = frame;
            info.input = _last_input;
            info.cbuf = _sync.GetLastSavedFrame().cbuf;
            info.buf = _sync.GetLastSavedFrame().buf;
            info.checksum = _sync.GetLastSavedFrame().checksum;
            _saved_frames.Push(info);

            if (frame - _last_verified == _check_distance)
            {
                // We've gone far enough ahead and should now start replaying frames.
                // Load the last verified frame and set the rollback flag to true.
                _sync.LoadFrame(_last_verified);

                _rollingback = true;
                while (!_saved_frames.Empty())
                {
                    _callbacks.OnAdvanceFrame();

                    // Verify that the checksumn of this frame is the same as the one in our
                    // list.
                    info = _saved_frames.Front();
                    _saved_frames.Pop();

                    if (info.frame != _sync.GetFrameCount())
                    {
                        throw new System.Exception(string.Format("Frame number {0} does not match saved frame number {1}", info.frame, frame));
                    }
                    int checksum = _sync.GetLastSavedFrame().checksum;
                    if (info.checksum != checksum)
                    {
                        throw new System.Exception(string.Format("Checksum for frame {0} does not match saved ({1} != {2})", frame, checksum, info.checksum));
                    }
                    Logger.Log("Checksum {0} for frame {1} matches.\n", checksum, info.frame);
                    info.buf = null; // free(info.buf);
                }
                _last_verified = frame;
                _rollingback = false;
            }

            return PUErrorCode.PU_OK;
        }
    }
}
