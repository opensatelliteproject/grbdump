using System;
using OpenSatelliteProject.GRB.Headers;
using OpenSatelliteProject.Tools;
namespace grbdump {
    class MSDUInfo {
        const long TIMEOUT = 15 * 60 * 60; // 15 minutes

        public int APID { get; set; }
        public long ReceivedTime { get; private set; }
        public string FileName { get; set; }
        public GRBGenericHeader GenericHeader { get; set; }
        public GRBImageHeader ImageHeader { get; set; }

        public bool Expired { 
            get {
                return LLTools.TimestampMS() - ReceivedTime > TIMEOUT;  
            }
        }

        public MSDUInfo() {
            ReceivedTime = LLTools.TimestampMS();
        }

        public void Refresh() {
            ReceivedTime = LLTools.TimestampMS();
        }
    }
}
