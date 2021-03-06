﻿using System;
using OpenSatelliteProject;
using System.Threading;
using System.Linq;
using System.Diagnostics;

namespace grbdump {
    class MainClass {
        static UdpReceiver udpReceiver;
        static Connector cn;

        static ChannelManager channel5, channel6;

        static MSDUManager msduManager;
        static FileHandlerManager fileHandlerManager;

        public static void Main (string[] args) {
            try {
                Process thisProc = Process.GetCurrentProcess();
                thisProc.PriorityClass = ProcessPriorityClass.RealTime;
            } catch (Exception e) {
                UIConsole.Error($"Failed changing process priority: {e}");
            }

			fileHandlerManager = new FileHandlerManager();
			msduManager = new MSDUManager(fileHandlerManager);
            channel5 = new ChannelManager(msduManager);
            channel6 = new ChannelManager(msduManager);
            // cn = new Connector();

            channel5.Start();
            channel6.Start();
            msduManager.Start();
            fileHandlerManager.Start();

            UIConsole.GlobalEnableDebug = true;

            /*cn = new Connector ();
            cn.ChannelDataAvailable += data => {
                data = data.Take(2042).ToArray();
                int vcid = (data[1] & 0x3F);
                if (vcid == 5) {
                    channel5.NewPacket(data);
                } else if (vcid == 6) {
                    channel6.NewPacket(data);
                } else {
                    UIConsole.Error($"Unknown VCID for GRB: {vcid}");
                }
            };
            cn.Start ();
            */
            udpReceiver = new UdpReceiver();
            udpReceiver.ChannelDataAvailable += data => {
                data = data.Take(2042).ToArray();
                int vcid = (data[1] & 0x3F);
                if (vcid == 5) {
                    channel5.NewPacket(data);
                } else if (vcid == 6) {
                    channel6.NewPacket(data);
                } else if (vcid == 63 ) {
                    // Fill Frame
                } else {
                    UIConsole.Error($"Unknown VCID for GRB: {vcid}");
                }
            };
            udpReceiver.Start();

            while (true) {
                Thread.Sleep (1000);
                Thread.Yield();
            }
        }
    }
}
