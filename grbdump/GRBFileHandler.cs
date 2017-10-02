using System;
using System.IO;
using OpenSatelliteProject;
using grbdump.GRB;

namespace grbdump {
    public static class GRBFileHandler {

        public static void HandleFile(string filename, GRBGenericHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName (filename);
            string f = Path.Combine (FileHandler.FinalFileFolder, ofilename);
            try {
                UIConsole.Log($"Moving File {filename} to {f}");
                File.Move(filename, f);
            } catch (IOException e) {
                UIConsole.Error(String.Format("Error moving file {0} to {1}: {2}", filename, f, e));
            }
        }

        public static void HandleFile(string filename, GRBImageHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName (filename);
            string f = Path.Combine (FileHandler.FinalFileFolder, ofilename);
            try {
                UIConsole.Log($"Moving File {filename} to {f}");
                File.Move(filename, f);
            } catch (IOException e) {
                UIConsole.Error(String.Format("Error moving file {0} to {1}: {2}", filename, f, e));
            }
        }
    }
}

