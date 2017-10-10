using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using OpenSatelliteProject;

namespace grbdump {
    public class Demuxer {
        readonly int FRAMESIZE = 2042;
        /// <summary>
        /// More than that, we will not count as loss, but as a corrupted frame.
        /// </summary>
        readonly int MAX_ACCOUTABLE_LOSSES = 1000;

        readonly Dictionary<int, OpenSatelliteProject.GRB.MSDU> temporaryStorage;

        int lastAPID;
        int lastFrame;
        int channelId;
        byte[] buffer;
		readonly MSDUManager msduManager;

        public int CRCFails { get; set; }
        public int Bugs { get; set; }
        public int Packets { get; set; }
        public int LengthFails { get; set; }
        public long FrameLoss { get; set; }
        public int FrameJumps { get; set; }

        /// <summary>
        /// Ignores the overflow-like jumps on Frame Loss counter
        /// </summary>
        /// <value><c>true</c> if ignore counter jump; otherwise, <c>false</c>.</value>
        public static bool IgnoreCounterJump { get; set; }

        static Demuxer() {
            IgnoreCounterJump = true;
        }

        public Demuxer(MSDUManager msduManager) {
            temporaryStorage = new Dictionary<int, OpenSatelliteProject.GRB.MSDU>();
            buffer = new byte[0];
            lastAPID = -1;
            lastFrame = -1;
            FrameLoss = 0;
            LengthFails = 0;
            CRCFails = 0;
            FrameJumps = 0;
            Bugs = 0;
            Packets = 0;
            this.msduManager = msduManager;
        }

        public Tuple<int, byte[]> CreatePacket(byte[] data) {
            int apid = -1;
            while (true) {
                if (data.Length < 14) {
                    return Tuple.Create(-1, data);
                }

                OpenSatelliteProject.GRB.MSDU msdu = OpenSatelliteProject.GRB.MSDU.parseMSDU(data);

                temporaryStorage[msdu.APID] = msdu;
                apid = msdu.APID;

                if (msdu.RemainingData.Length > 0 || msdu.Full) {
                    data = msdu.RemainingData;
                    msdu.RemainingData = new byte[0];
                    msduManager.FinishMSDU(msdu);
                    temporaryStorage.Remove(msdu.APID);
                    apid = -1;
                } else {
                    break;
                }
            }
            return Tuple.Create(apid, new byte[0]);
        }

        public void ParseBytes(byte[] data) {
            uint counter;
            bool replayFlag;
            bool ovfVcnt;
            bool ovfVcntProblem;
            bool frameJump;

            if (data.Length < FRAMESIZE) {
                throw new Exception(String.Format("Not enough data. Expected {0} and got {1}", FRAMESIZE, data.Length));
            }

            channelId = (data[1] & 0x3F);

            byte[] cb = data.Skip(2).Take(4).ToArray();

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(cb);
            }

            cb[0] = 0x00;


            counter = BitConverter.ToUInt32(cb, 0);
            //counter &= 0xFFFFFF00;
            counter >>= 8;
            replayFlag = (data[5] & 0x80) > 0;

            if (replayFlag) {
                UIConsole.Debug("Replay Flag set. Skipping packet.");
                return;
            }

            if (counter - lastFrame - 1 == -1) {
                UIConsole.Warn("Last packet same ID as the current one but no replay bit set! Skipping packet.");
                return;
            }

            frameJump = lastFrame > counter;
            ovfVcnt = frameJump && counter == 0;
            ovfVcntProblem = ovfVcnt && (0xFFFFFF - lastFrame) + counter - 1 > 0;

            if (frameJump && !ovfVcnt) {
                UIConsole.Warn($"Frame Jump occured. Current Frame: {counter} Last Frame: {lastFrame}");
                if (lastAPID != -1) {
                    temporaryStorage[lastAPID].FrameLost = true;
                }
            } else if (lastFrame != -1 && lastFrame + 1 != counter && !ovfVcnt) {
                UIConsole.Error(String.Format("Lost {0} frames. Last Frame #{1} - Current Frame #{2} on VCID {3}", counter - lastFrame - 1, lastFrame, counter, channelId));
                if (lastAPID != -1) {
                    temporaryStorage[lastAPID].FrameLost = true;
                }
            } else if (!IgnoreCounterJump && lastFrame != -1 && ovfVcntProblem) {
                UIConsole.Error(String.Format("Lost {0} frames. Last Frame #{1} - Current Frame #{2} on VCID {3}", (0xFFFFFF - lastFrame) + counter  - 1, lastFrame, counter, channelId));
                if (lastAPID != -1) {
                    temporaryStorage[lastAPID].FrameLost = true;
                }
            }

            if (ovfVcntProblem && IgnoreCounterJump || frameJump && IgnoreCounterJump) {
                UIConsole.Warn($"Frame Jump detected from {lastFrame} to {counter} on VCID {channelId} but IgnoreCounterJump is set to true. Ignoring...");
            }

            if (lastFrame != -1) {
                if (frameJump && !ovfVcnt) {
                    // manager.FrameLoss++;
                } else if (!IgnoreCounterJump && ovfVcnt) {
                    int losses = (int) Math.Abs((0xFFFFFF - lastFrame) + counter - 1);
                    if (losses < MAX_ACCOUTABLE_LOSSES) {
                        FrameLoss += losses;
                    } else {
                        UIConsole.Warn($"Frame Lost ({losses}) in this section is higher than max accountable losses. Not accounting for it (probably corrupt frame).");
                    }
                } else if (!ovfVcnt) {
                    int losses = (int) Math.Abs(counter - lastFrame - 1);
                    if (losses < MAX_ACCOUTABLE_LOSSES) {
                        FrameLoss += losses;
                    } else {
                        UIConsole.Warn($"Frame Lost ({losses}) in this section is higher than max accountable losses. Not accounting for it (probably corrupt frame).");
                    }
                }
            }

            if (frameJump && !ovfVcnt) {
                FrameJumps++;
            }

            if (lastFrame < counter || ovfVcnt || frameJump) {
                lastFrame = (int)counter;
            } else {
                UIConsole.Warn($"LastFrame is bigger than currentFrame ({lastFrame} > {counter}). Not changing current number...");
            }

            cb = data.Skip(6).Take(2).ToArray();
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(cb);
            }

            int fhp = BitConverter.ToUInt16(cb, 0) & 0x7FF;

            data = data.Skip(8).ToArray();

            // Data is now TP_PDU
            var p = Tuple.Create(0, new byte[0]);
            if (fhp != 2047) { // Has a packet start
                if (lastAPID == -1 && buffer.Length > 0) {
                    //  There was not enough data to packetize last time. So lets fill the buffer until the fhp and create packet.
                    if (fhp > 0) {
                        buffer = buffer.Concat(data.Take(fhp)).ToArray();
                        data = data.Skip(fhp).ToArray();
                        fhp = 0;
                    }           

                    p = CreatePacket(buffer);
                    lastAPID = p.Item1;
                    buffer = p.Item2;
                } 

                if (lastAPID != -1) {
                    if (fhp > 0) {
                        temporaryStorage[lastAPID].addDataBytes(buffer.Concat(data.Take(fhp)).ToArray());
                        data = data.Skip(fhp).ToArray();
                        fhp = 0;
                    }

                    if (!temporaryStorage[lastAPID].Full && !temporaryStorage[lastAPID].FrameLost && lastAPID != 2047) {
                        Bugs++;
                        StackFrame callStack = new StackFrame(0, true);
                        UIConsole.Debug(String.Format("Problem at line {0} in file {1}! Not full! Check code for bugs!", callStack.GetFileLineNumber(), callStack.GetFileName()));
                    }
                    msduManager.FinishMSDU(temporaryStorage[lastAPID]);
                    temporaryStorage.Remove(lastAPID);
                    lastAPID = -1;
                }

                buffer = buffer.Concat(data.Skip(fhp)).ToArray();
                p = CreatePacket(buffer);
                lastAPID = p.Item1;
                buffer = p.Item2;
            } else {
                if (buffer.Length > 0 && lastAPID != -1) {
                    buffer = buffer.Concat(data).ToArray();
                    p = CreatePacket(buffer);
                    lastAPID = p.Item1;
                    buffer = p.Item2;
                } else if (lastAPID == -1) {
                    buffer = buffer.Concat(data).ToArray();
                    p = CreatePacket(buffer);
                    lastAPID = p.Item1;
                    buffer = p.Item2;
                } else if (buffer.Length > 0) {
                    UIConsole.Error("EDGE CASE! PLEASE REPORT THIS MESSAGE");
                } else {
                    temporaryStorage[lastAPID].addDataBytes(data);
                }
            }
        }
    }
}

