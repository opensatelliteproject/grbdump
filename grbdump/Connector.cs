using System;
using System.Threading;
using OpenSatelliteProject;
using System.Net;
using System.Net.Sockets;

namespace grbdump {
    class Connector {
        public static int ChannelDataServerPort { get; set; }
        public static string ChannelDataServerName { get; set; }

        static Connector() {
            ChannelDataServerName = "localhost";
            ChannelDataServerPort = 5001;
        }

        #region Delegate
        public delegate void ChannelDataEvent(byte[] data);
        #endregion
        #region Event
        public event ChannelDataEvent ChannelDataAvailable;
        #endregion

        Thread channelDataThread;
        bool channelDataThreadRunning;

        public Connector () {
            channelDataThread = new Thread(new ThreadStart(ChannelDataLoop)) {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };
        }

        public void Start() {
            channelDataThreadRunning = true;
            channelDataThread.Start();
        }

        ~Connector() {
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
                UIConsole.Log("Channel Data Loop started");
                byte[] buffer = new byte[2042];

                IPHostEntry ipHostInfo = Dns.GetHostEntry(ChannelDataServerName);
                IPAddress ipAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
                foreach (IPAddress ip in ipHostInfo.AddressList) {
                    if (ip.AddressFamily != AddressFamily.InterNetworkV6) {
                        ipAddress = ip;
                        break;
                    }
                }
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, ChannelDataServerPort);
                Socket sender = null;

                while (channelDataThreadRunning) {

                    bool isConnected = true;
                    UIConsole.Log("Channel Data Thread connect");
                    try {
                        sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                            ReceiveTimeout = 5000
                        };
                        sender.Connect(remoteEP);
                        isConnected = true;
                        UIConsole.Log($"Socket connected to {sender.RemoteEndPoint}");
                        int nullReceive = 0;
                        while (isConnected) {
                            try {
                                var receivedBytes = sender.Receive(buffer);
                                if (receivedBytes < buffer.Length && receivedBytes != 0) {
                                    UIConsole.Error("Received less bytes than channel data!");
                                    Thread.Sleep(200);
                                    nullReceive = 0;
                                } else  if (receivedBytes == 0) {
                                    nullReceive++;
                                    if (nullReceive == 5) {
                                        UIConsole.Error("Cannot reach server. Dropping connection!");
                                        isConnected = false;
                                        sender.Shutdown(SocketShutdown.Both);
                                        sender.Disconnect(false);
                                        sender.Close();
                                    }
                                } else {
                                    nullReceive = 0;
                                    this.PostChannelData(buffer);
                                }
                            } catch (ArgumentNullException ane) {
                                UIConsole.Error($"ArgumentNullException : {ane}");
                                isConnected = false;
                            } catch (SocketException) {
								isConnected = false;
                            } catch (Exception e) {
                                UIConsole.Error($"Unexpected exception : {e}");
                                isConnected = false;
                            }

                            DataConnected = isConnected;

                            if (!channelDataThreadRunning) {
                                break;
                            }
                        }

                        sender.Shutdown(SocketShutdown.Both);
                        sender.Disconnect(false);
                        sender.Close();

                    } catch (ArgumentNullException ane) {
                        UIConsole.Error($"ArgumentNullException : {ane}");
                    } catch (SocketException se) {
                        UIConsole.Error($"SocketException : {se}");
                    } catch (Exception e) {
                        UIConsole.Error($"Unexpected exception : {e}");
                    }
                    if (channelDataThreadRunning) {
                        UIConsole.Warn("Socket closed. Waiting 1s before trying again.");
                        Thread.Sleep(1000);
                    }
                }

                UIConsole.Debug("Requested to close Channel Data Thread!");
                try {
                    if (sender != null) {
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Disconnect(false);
                        sender.Close();
                    }
                } catch (Exception e) {
                    UIConsole.Debug($"Exception thrown when closing socket: {e} Ignoring.");
                }

                UIConsole.Log("Channel Data Thread closed.");
            } catch (Exception e) {
                CrashReport.Report (e);
            }
        }

    }
}

