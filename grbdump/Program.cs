using System;
using OpenSatelliteProject;
using System.Threading;
using System.Linq;
using System.Diagnostics;

namespace grbdump {
    class MainClass {
        static Demuxer grbDemuxer5;
        static Demuxer grbDemuxer6;
        static Connector cn;
        public static void Main (string[] args) {
            try {
                Process thisProc = Process.GetCurrentProcess();
                thisProc.PriorityClass = ProcessPriorityClass.High;
            } catch (Exception e) {
                UIConsole.Error($"Failed changing process priority: {e}");
            }
            Console.WriteLine ("Hello World!");
            UIConsole.GlobalEnableDebug = true;
            grbDemuxer5 = new Demuxer ();
            grbDemuxer6 = new Demuxer ();
            cn = new Connector ();
            cn.ChannelDataAvailable += data => {
                data = data.Take(2042).ToArray();
                int vcid = (data[1] & 0x3F);
                if (vcid == 5) {
                    grbDemuxer5.ParseBytes(data);
                } else if (vcid == 6) {
                    grbDemuxer6.ParseBytes(data);
                } else {
                    UIConsole.Error($"Unknown VCID for GRB: {vcid}");
                }
            };
            cn.Start ();

            while (true) {
                Thread.Sleep (1000);
            }
        }
    }
}
