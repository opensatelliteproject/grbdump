using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenSatelliteProject;
using OpenSatelliteProject.GRB;
using OpenSatelliteProject.GRB.Headers;
using OpenSatelliteProject.GRB.Product;
using OpenSatelliteProject.IMTools;

namespace grbdump {
    /// <summary>
    /// GRB Data Holder
    /// </summary>
    /// <threadsafety static="false" instance="true"/>
    public class GrbData {

        /// <summary>
        /// If enabled save a PNG Image file for any image that arrives.
        /// </summary>
        /// <value><c>true</c> for saving png file; otherwise, <c>false</c>.</value>
        public static bool SavePNG { get; set; }

        /// <summary>
        /// If enabled save a PNG Image file for any image that arrives.
        /// </summary>
        /// <value><c>true</c> for saving jpg file; otherwise, <c>false</c>.</value>
        public static bool SaveJPG { get; set; }

        /// <summary>
        /// If enabled save a PGM Image file for any image that arrives.
        /// </summary>
        /// <value><c>true</c> for saving pgm file; otherwise, <c>false</c>.</value>
        public static bool SavePGM { get; set; }

        /// <summary>
        /// If enabled save Data Quality Factor file for any image that arrives.
        /// </summary>
        /// <value><c>true</c> for saving dqf file; otherwise, <c>false</c>.</value>
        public static bool SaveDQF { get; set; }

        public int APID { get; private set; }
        public string Title { get; private set; }
        public string Filename { get; private set; }
        public string ProductFolder { get; private set; }
        public ulong Epoch { get; private set; }

        ImageAssembler DQF { get; set; }
        ImageAssembler ImageCache { get; set; }
        ImageAssembler FullImageCache { get; set; }
        ImageAssembler FullDQFCache { get; set; }

        string Metadata { get; set; }
        public byte[] Data { get; set; }
        readonly object syncLock;

        static GrbData() {
            SaveJPG = false;
            SavePNG = true;
            SavePGM = true;
            SaveDQF = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:grbdump.GrbData"/> class.
        /// </summary>
        /// <param name="apid">APID of the Data</param>
        /// <param name="epoch">Epoch in microseconds of the data</param>
        public GrbData(int apid, ulong epoch) {
            APID = apid;
            ProductFolder = Products.GetFolderByAPID(APID);
            Epoch = epoch;
            DQF = null;
            ImageCache = null;
            FullDQFCache = null;
            FullImageCache = null;
            Metadata = null;
            Title = null;
            Filename = $"{Epoch}.nc";
            syncLock = new object();
        }

        /// <summary>
        /// Parses a metadata file
        /// </summary>
        /// <param name="filename">Filename.</param>
        public void ParseMetadata(string filename) {
            lock (syncLock) {
                try {
                    Metadata = File.ReadAllText(filename);
                    ParseMetadataFromText(Metadata);
                } catch (Exception e) {
                    UIConsole.Error($"Error parsing metadata file {filename}: {e.Message}");
                    // UIConsole.Debug($"{e}");
                }
            }
        }
        void ParseMetadataFromText(string text) {
            Metadata = text;
            var doc = XDocument.Parse(Metadata);
            Title = Tools.FindAttrValue(doc, "title");
            Filename = Tools.FindAttrValue(doc, "dataset_name");
        }

        /// <summary>
        /// Stores plain data
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <param name="header">Header.</param>
        public void ParseData(string filename, GRBGenericHeader header) {
            lock (syncLock) {
                try {
                    Data = File.ReadAllBytes(filename);
                } catch (Exception e) {
                    UIConsole.Error($"Error parsing data file {filename}: {e.Message}");
                    UIConsole.Debug($"{e}");
                }
            }
        }

        /// <summary>
        /// Stores plain data
        /// </summary>
        /// <param name="data">Data to Store.</param>
        public void ParseData(byte[] data) {
            lock (syncLock) {
                Data = data;
            }
        }

        /// <summary>
        /// Processes a new image part
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <param name="header">Header.</param>
        public void NewImage(string filename, GRBImageHeader header) {
            lock (syncLock) {
                if (ImageCache == null) {
                    if (SaveDQF) {
                        DQF = new ImageAssembler((int)header.width, (int)header.height, Epoch);
                    }
                    ImageCache = new ImageAssembler((int)header.width, (int)header.height, Epoch);
                }

                if (SaveDQF) {
                    // Separate DQF
                    string dqfFilename = $"{filename}.dqf";
                    try {
                        byte[] buffer = File.ReadAllBytes(filename);
                        buffer = buffer.Skip((int)header.dqfOffset).ToArray();
                        File.WriteAllBytes(dqfFilename, buffer);
                    } catch (Exception e) {
                        UIConsole.Error($"Error slicing DQF file to {filename}.dqf: {e.Message}");
                        UIConsole.Debug($"{e}");
                    }

                    DQF.AppendJ2K(dqfFilename);
                    try {
                        File.Delete(dqfFilename);
                    } catch (IOException e) {
                        UIConsole.Error($"Error erasing file {filename}: {e.Message}");
                        UIConsole.Debug($"{e}");
                    }
                }

                ImageCache.AppendJ2K(filename);

                try {
                    File.Delete(filename);
                } catch (IOException e) {
                    UIConsole.Error($"Error erasing file {filename}: {e.Message}");
                    UIConsole.Debug($"{e}");
                }

                if (ImageCache.Done) {
                    ProcessBigImage(header);
                }
            }
        }

        public void AddMetadataFrom(GrbData grbData) {
            Metadata = grbData.Metadata;
            try {
                ParseMetadataFromText(Metadata);
            } catch (Exception e) {
                UIConsole.Error($"Error parsing metadata: {e.Message}");
            }
        }

        public void AddDataFrom(GrbData grbData) {
            FullDQFCache = grbData.FullDQFCache;
            FullImageCache = grbData.FullImageCache;
            Data = grbData.Data;
        }

        /// <summary>
        /// Saves this instance to disk
        /// </summary>
        /// <returns>Async Task of saving</returns>
        public async Task Save() {
            string folder = Path.Combine(FileHandler.FinalFileFolder, ProductFolder);
            if (Filename == null) {
                Filename = $"{Epoch}.nc";
            }

            try {
                Directory.CreateDirectory(folder);
            } catch (IOException e) {
                UIConsole.Error($"Cannot create directory {folder}: {e}");
            }

            string dqfFilename = Filename.Replace(".nc", ".dqf.pgm");
            string imgFilename = Filename.Replace(".nc", ".pgm");
            string metaFilename = Filename.Replace(".nc", ".xml");
            string dataFilename = Filename.Replace(".nc", ".bin");

            if (Title == null) {
                Title = Products.GetNameByAPID(APID);
            }

            UIConsole.Log($"New Product: {Title}");

            if (Metadata != null) {
                metaFilename = Path.Combine(folder, metaFilename);
                UIConsole.Debug($"Saving file {metaFilename}");
                File.WriteAllText(metaFilename, Metadata);
            }

            if (Data != null) {
                dataFilename = Path.Combine(folder, dataFilename);
                UIConsole.Debug($"Saving file {dataFilename}");
                File.WriteAllBytes(dataFilename, Data);
            }

            if (FullDQFCache != null && SaveDQF) {
                dqfFilename = Path.Combine(folder, dqfFilename);
                UIConsole.Debug($"Saving file {dqfFilename}");
                await FullDQFCache.AsyncSavePGM(dqfFilename);
            }

            if (FullImageCache != null) {
                if (SavePGM) {
                    imgFilename = Path.Combine(folder, imgFilename);
                    UIConsole.Debug($"Saving file {imgFilename}");
                    await FullImageCache.AsyncSavePGM(imgFilename);
                }
                if (SaveJPG) {
                    string jpgFilename = imgFilename.Replace(".pgm", ".jpg");
                    jpgFilename = Path.Combine(folder, jpgFilename);
                    UIConsole.Debug($"Saving file {jpgFilename}");
                    await FullImageCache.AsyncSaveJPG(jpgFilename);
                }
                if (SavePNG) {
                    string pngFilename = imgFilename.Replace(".pgm", ".png");
                    pngFilename = Path.Combine(folder, pngFilename);
                    UIConsole.Debug($"Saving file {pngFilename}");
                    await FullImageCache.AsyncSavePNG(pngFilename);
                }
            }
        }

        void ProcessBigImage(GRBImageHeader header) {
            if (FullImageCache == null) {
                var product = Products.GetProductByAPID(APID);
                var imsz = (ImageSize)product.Meta[1];
                FullImageCache = new ImageAssembler(imsz.Width, imsz.Height, Epoch);
                if (SaveDQF) {
                    FullDQFCache = new ImageAssembler(imsz.Width, imsz.Height, Epoch);
                }
            }

            FullImageCache.DrawAt(ImageCache.Image, (int)header.ulX, (int)header.ulY + (int)header.rowOffset, true); // rowOffset to int might be bad
            if (SaveDQF) {
                FullDQFCache.DrawAt(DQF.Image, (int)header.ulX, (int)header.ulY + (int)header.rowOffset, true); // rowOffset to int might be bad
            }
            DQF = null;
            ImageCache = null;
        }
    }
}
