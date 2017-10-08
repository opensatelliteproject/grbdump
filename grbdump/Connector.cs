using System;
using System.Threading;
using OpenSatelliteProject;
using System.Net;
using System.Net.Sockets;

namespace grbdump {
    public class Connector {
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

        private Thread channelDataThread;
        private bool channelDataThreadRunning;

        public Connector () {
            channelDataThread = new Thread(new ThreadStart(channelDataLoop)) {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
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

        private void postChannelData(object data) {
            ChannelDataAvailable?.Invoke((byte[])data);
        }

        private void channelDataLoop() {
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
                        sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sender.ReceiveTimeout = 5000;
                        sender.Connect(remoteEP);
                        isConnected = true;
                        UIConsole.Log(String.Format("Socket connected to {0}", sender.RemoteEndPoint.ToString()));
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
                                    this.postChannelData(buffer);
                                }
                            } catch (ArgumentNullException ane) {
                                UIConsole.Error(String.Format("ArgumentNullException : {0}", ane.ToString()));
                                isConnected = false;
                            } catch (SocketException se) {
                                // That's usually timeout.  I would say that is best to handle and show some message
                                // But for now, that would make it confusing for the users. So let's keep without a notice.
                                //UIConsole.GlobalConsole.Error(String.Format("SocketException : {0}", se.ToString()));
                                isConnected = false;
                            } catch (Exception e) {
                                UIConsole.Error(String.Format("Unexpected exception : {0}", e.ToString()));
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
                        UIConsole.Error(String.Format("ArgumentNullException : {0}", ane.ToString()));
                    } catch (SocketException se) {
                        UIConsole.Error(String.Format("SocketException : {0}", se.ToString()));
                    } catch (Exception e) {
                        UIConsole.Error(String.Format("Unexpected exception : {0}", e.ToString()));
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
                    UIConsole.Debug(String.Format("Exception thrown when closing socket: {0} Ignoring.", e.ToString()));
                }

                UIConsole.Log("Channel Data Thread closed.");
            } catch (Exception e) {
                CrashReport.Report (e);
            }
        }

    }
}

