using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenSatelliteProject;

namespace grbdump {
    class UdpReceiver {

        static readonly byte[] SyncMark = { 0x1A, 0xCF, 0xFC, 0x1D };

        public static int ChannelDataServerPort { get; set; }
        public static string ChannelDataServerName { get; set; }

        static UdpReceiver() {
            ChannelDataServerPort = 1234;
        }

        #region Delegate
        public delegate void ChannelDataEvent(byte[] data);
        #endregion
        #region Event
        public event ChannelDataEvent ChannelDataAvailable;
        #endregion

        Thread channelDataThread;
        bool channelDataThreadRunning;

        public UdpReceiver() {
            channelDataThread = new Thread(new ThreadStart(ChannelDataLoop)) {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };
        }

        public void Start() {
            channelDataThreadRunning = true;
            channelDataThread.Start();
        }

        ~UdpReceiver() {
            Stop();
        }

        #region Properties

        public bool StatisticsConnected { get; set; }
        public bool DataConnected { get; set; }

        #endregion

        public void Stop() {
            channelDataThreadRunning = false;

            if (channelDataThread != null) {
                channelDataThread.Join();
                channelDataThread = null;
            }
        }

        void PostChannelData(object data) {
            ChannelDataAvailable?.Invoke((byte[])data);
        }

        void ChannelDataLoop() {
            try {
                UIConsole.Log($"UDP Channel Data Loop started at port {ChannelDataServerPort}");
                UdpClient udpClient = new UdpClient(ChannelDataServerPort);
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                udpClient.Client.ReceiveTimeout = 1000;
                while (channelDataThreadRunning) {
                    try {
                        byte[] buffer = udpClient.Receive(ref RemoteIpEndPoint);
                        switch (buffer.Length) {
                            case 5380:
                            case 5384:
                            case 7274:
                            case 7278:
                                HandleBBFrame(buffer);
                                break;
                            case 2052:
                            case 2048:
                                HandleCADU(buffer);
                                break;
                            default:
                                UIConsole.Error($"Received invalid frame size: {buffer.Length}");
                                break;
                        }
                    } catch (Exception e) {
                        // TODO: Handle
                        UIConsole.Error(e.ToString());
                    }
                }

                UIConsole.Log("UDP Channel Data Thread closed.");
            } catch (Exception e) {
                CrashReport.Report(e);
            }
        }

        long lastPacketNumber = -1;
        List<byte> lastFrame = new List<byte>();
        byte[] lastData = new byte[0];

        int FindSyncMark(byte[] data) {
            int pos = -1;

            for (int i = 0; i < data.Length - SyncMark.Length; i++) {
                bool ok = true;
                for (int z = 0; z < SyncMark.Length; z++) {
                    ok &= data[i + z] == SyncMark[z]; 
                }
                if (ok) {
                    pos = i;
                    break;
                }
            }

            return pos;
        }

        void HandleBBFrame(byte[] data) {
            long counter = -1;
            if (data.Length == 5384 || data.Length == 7278) {
                // Ayecka Packet Count is broken
                byte[] countData = data.Take(4).ToArray();
                data = data.Skip(4).ToArray();

                if (countData[0] == 0xB8) { // DVB-S3 Layer 3 Adaptation Header
                    counter = countData[3];
                }
            }

            bool QPSK = data.Length == 7274;

            if (lastPacketNumber != -1 && counter != -1 && lastPacketNumber != 255) {
                if (lastPacketNumber == counter) {
                    UIConsole.Warn("Packet arrived duplicated! Dropping.");
                    return;
                }
                if (lastPacketNumber > counter) {
                    UIConsole.Warn($"Packet arrived out of order! Dropping. - Last: {lastPacketNumber}, Current: {counter}");
                    return;
                }
                if (lastPacketNumber + 1 != counter) {
                    long missingPackets = counter - lastPacketNumber + 1;
                    UIConsole.Warn($"Missing {missingPackets} packets! - Last: {lastPacketNumber}, Current: {counter}");
                }
            }

            lastPacketNumber = counter;

            byte[] bbHeader = data.Take(10).ToArray();
            data = data.Skip(10).ToArray();
            // TODO: Maybe parse BB Header?
            lastData = lastData.Concat(data).ToArray();
            if (!lastFrame.Any()) {
                while (lastData.Length > 0) {
                    int pos = FindSyncMark(lastData);
                    if (pos == -1) {
                        return;
                    }
                    lastData = lastData.Skip(pos).ToArray();
                    byte[] frameData = lastData.Length > 2048 ? lastData.Take(2048).ToArray() : lastData;
                    lastData = lastData.Skip(frameData.Length).ToArray();
                    lastFrame.AddRange(frameData);
                    if (lastFrame.Count() == 2048) {
                        HandleCADU(lastFrame.ToArray());
                        lastFrame.Clear();
                    }
                }
            } else {
                int remainingBytes = 2048 - lastFrame.Count();
                byte[] frameData = lastData.Length > remainingBytes ? lastData.Take(remainingBytes).ToArray() : lastData;
                lastData = lastData.Length > remainingBytes ? lastData.Skip(remainingBytes).ToArray() : new byte[0];
                lastFrame.AddRange(frameData);
                if (lastFrame.Count() == 2048) {
                    HandleCADU(lastFrame.ToArray());
                    lastFrame.Clear();
                }
            }

        }

        long lastCaduNumber = -1;

        void HandleCADU(byte[] data) {
            long caduNumber = -1; 
            if (data.Length == 2052) {
                byte[] counter = data.Take(4).ToArray();
                data = data.Skip(4).ToArray();
                caduNumber = BitConverter.ToUInt32(counter, 0);
            }

            if (lastCaduNumber != -1 && caduNumber != -1) {
                if (lastCaduNumber == caduNumber) {
                    UIConsole.Warn("CADU Packet arrived duplicated! Dropping.");
                    return;
                }
                if (lastCaduNumber > caduNumber) {
                    UIConsole.Warn("CADU Packet arrived out of order! Dropping.");
                    return;
                }
                if (lastCaduNumber + 1 != caduNumber) {
                    long missingPackets = caduNumber - lastPacketNumber + 1;
                    UIConsole.Warn($"Missing {missingPackets} CADU packets!");
                }
            }

            if (FindSyncMark(data) != 0) {
                UIConsole.Error("Corrupted CADU data!");
                return;
            }

            PostChannelData(data.Skip(4).ToArray());
        }
    }
}
