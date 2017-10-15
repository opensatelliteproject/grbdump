using System;
using System.IO;
using OpenSatelliteProject.GRB.Headers;
using OpenSatelliteProject;
using System.Collections.Generic;
using OpenSatelliteProject.IMTools;
using System.Threading.Tasks;
using OpenSatelliteProject.GRB.Product;
using OpenSatelliteProject.GRB;
using System.Linq;

namespace grbdump {
    static class GRBFileHandler {
        public static void HandleFile(string filename, GRBGenericHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName(filename);

            if (Tools.IsXML(filename)) {
                ofilename = ofilename.Replace(".grb", ".xml");
            }

            string productFolder = Products.GetFolderByAPID(header.apid);
            string f = Path.Combine(FileHandler.FinalFileFolder, productFolder);
            try {
                Directory.CreateDirectory(f);
            } catch (IOException e) {
                UIConsole.Error($"Cannot create directory {f}: {e}");
            }
            f = Path.Combine(f, ofilename);

            if (File.Exists(f)) {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
                string ext = Path.GetExtension(f);
                string append = $"--dup-{timestamp}{ext}";
                f = f.Replace(ext, append);
            }

            try {
                File.Move(filename, f);
            } catch (IOException e) {
                UIConsole.Error($"Error moving file {filename} to {f}: {e}");
            }
        }

        static readonly Dictionary<string, ImageAssembler> ImageCache = new Dictionary<string, ImageAssembler>();
        static readonly Dictionary<string, ImageAssembler> DQFCache = new Dictionary<string, ImageAssembler>();
        static readonly Dictionary<string, ImageAssembler> BigImageCache = new Dictionary<string, ImageAssembler>();
        static readonly Dictionary<int, ulong> APIDStamp = new Dictionary<int, ulong>();

        public static void HandleFile(string filename, GRBImageHeader header) {
            string dir = Path.GetDirectoryName(filename);
            string ofilename = header.filename ?? Path.GetFileName(filename);

            // Separate DQF
            string dqfFilename = $"{filename}.dqf";
            try {
                byte[] buffer = File.ReadAllBytes(filename);
                buffer = buffer.Skip((int)header.dqfOffset).ToArray();
                File.WriteAllBytes(dqfFilename, buffer);
            } catch (Exception) { }


            string productFolder = Products.GetFolderByAPID(header.apid);
            string bPath = productFolder;
            string zPath = Path.Combine(bPath, $"{header.epoch:D16}", $"{header.sequence:D8}");
            string f = Path.Combine(FileHandler.FinalFileFolder, zPath);

            try {
                Directory.CreateDirectory(Path.Combine(FileHandler.FinalFileFolder, bPath, $"{header.epoch:D16}"));
            } catch (IOException e) {
                UIConsole.Error($"Cannot create directory {bPath}: {e}");
            }

            if (!ImageCache.ContainsKey(zPath)) {
                ImageCache.Add(zPath, new ImageAssembler((int)header.width, (int)header.height));
                DQFCache.Add(zPath, new ImageAssembler((int)header.width, (int)header.height));
            }

            ImageCache[zPath].AppendJ2K(filename);
            DQFCache[zPath].AppendJ2K(dqfFilename);

            try {
                File.Delete(filename);
                File.Delete(dqfFilename);
            } catch (IOException e) {
                UIConsole.Error($"Error erasing file {filename}: {e}");
            }

            if (ImageCache[zPath].Done) {
                // UIConsole.Log ($"New image at {f}");
                // ImageCache [zPath].SavePGM ($"{f}.pgm");
                // ImageCache [zPath].SaveJPG ($"{f}.jpg");
                // File.WriteAllText($"{f}.txt", header.ToString());
                ProcessBigImage(bPath, ImageCache[zPath], header);
                ImageCache.Remove(zPath);
                // FIXME: Discarding DQF
                // DQFCache[zPath].SavePGM($"{f}.dqf.pgm");
                DQFCache.Remove(zPath);
            }
        }

        static void ProcessBigImage(string bPath, ImageAssembler segment, GRBImageHeader header) {
            int apid = header.apid;
            ulong currentEpoch = header.epoch;
            string imKey = $"{apid:X3}-{currentEpoch}";
            var product = Products.GetProductByAPID(apid);
            var imsz = (ImageSize)product.Meta[1];

            if (APIDStamp.ContainsKey(apid) && APIDStamp[apid] != currentEpoch) {
                ulong oldEpoch = APIDStamp[apid];
                string oldImKey = $"{apid:X3}-{oldEpoch}";
                string outfolder = Path.Combine(FileHandler.FinalFileFolder, bPath, oldEpoch.ToString());
                Task.Run(async () => {
                    var imas = BigImageCache[oldImKey];
                    // UIConsole.Log($"Saving{outfolder}.pgm");
                    await imas.AsyncSavePGM($"{outfolder}.pgm");
                    // UIConsole.Log($"Saving{outfolder}.png");
                    await imas.AsyncSavePNG($"{outfolder}.png");
                    BigImageCache[oldImKey] = null;
                    UIConsole.Log($"New {product.Name} at {outfolder}.pgm");
                });

                BigImageCache[imKey] = new ImageAssembler(imsz.Width, imsz.Height, currentEpoch);
                APIDStamp[apid] = currentEpoch;
                // UIConsole.Debug($"Starting for {imKey} with expected size ({imsz.Width}, {imsz.Height})");
            } else if (!BigImageCache.ContainsKey(imKey)) {
                BigImageCache[imKey] = new ImageAssembler(imsz.Width, imsz.Height, currentEpoch);
                APIDStamp[apid] = currentEpoch;
                // UIConsole.Debug($"Starting for {imKey} with expected size ({imsz.Width}, {imsz.Height})");
            }
            // UIConsole.Debug ($"{header.apid:X3} - Drawing {header.sequence} at {header.ulX}, {header.ulY}");
            BigImageCache[imKey].DrawAt(segment.Image, (int)header.ulX, (int)header.ulY + (int)header.rowOffset, true); // rowOffset to int might be bad
        }
    }
}

