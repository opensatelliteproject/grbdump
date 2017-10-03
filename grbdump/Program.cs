using System;
using OpenSatelliteProject;
using System.Threading;
using System.Linq;

namespace grbdump {
    class MainClass {
        static GRBDemuxer grbDemuxer;
        static Connector cn;
        public static void Main (string[] args) {
            Console.WriteLine ("Hello World!");
            UIConsole.GlobalEnableDebug = true;
            grbDemuxer = new GRBDemuxer ();
            cn = new Connector ();
            cn.ChannelDataAvailable += (byte[] data) => {
                data = data.Take(2042).ToArray();
                //UIConsole.Log("Received Packet");
                //data.Print();
                int vcid = (data[1] & 0x3F);
                int vcnt = (data[2] << 16 | data[3] << 8 | data[4]);
                // UIConsole.Log($"Packet VCID: {vcid}");
                // UIConsole.Log($"Packet Count : {vcnt}");
                if (vcid != 5) {
                    UIConsole.Warn("Skipping");
                } else {
                    grbDemuxer.ParseBytes(data);
                }
            };
            cn.Start ();

            while (true) {
                Thread.Sleep (1000);
            }
        }
    }
}
