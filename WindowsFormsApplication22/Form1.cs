using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace WindowsFormsApplication22
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        static UInt32[] edc_lut = new UInt32[256];
        static Byte[] ecc_f_lut = new Byte[256];
        static Byte[] ecc_b_lut = new Byte[256];

        private void button1_Click(object sender, EventArgs e)
        {
            UInt32 i, j, k;

            for (i = 0; i < 256; i++)
            {
                j = (UInt32)((i << 1) ^ ((i & 0x80) != 0 ? 0x11d : 0));
                ecc_f_lut[i] = (byte)j;
                ecc_b_lut[i ^ j] = (byte)i;
                k = i;

                for (j = 0; j < 8; j++)
                {
                    k = (k >> 1) ^ ((k & 1) != 0 ? 0xd8018001 : 0);
                }
                edc_lut[i] = k;
            }

            var LOG = new List<string>();

            var ISO = new BinaryReader(File.OpenRead(textBox1.Text));

            var cursor = 0;

            while (ISO.BaseStream.Position != ISO.BaseStream.Length)
            {
                ISO.BaseStream.Position = cursor;
                var buffer = new BinaryReader(new MemoryStream(ISO.ReadBytes(2352)));

                buffer.BaseStream.Position = 0x12;

                var size = (buffer.ReadByte() & 0x20) != 0 ? 2332 : 2056;

                buffer.BaseStream.Position = 0x10;

                var sector = buffer.ReadBytes(size);

                var innerEDC = buffer.ReadInt32();

                buffer.BaseStream.Position = 0x0C;
                var sectorECCP = buffer.ReadBytes(2064);

                buffer.BaseStream.Position = 0x0C;
                var sectorECCQ = buffer.ReadBytes(2236);

                var edc = computeEDC(sector);

                var dest = new byte[2352];


                UInt32 major_count, minor_count, major_mult, minor_inc;
                major_count = 86;
                minor_count = 24;
                major_mult = 2;
                minor_inc = 86;

                //
                var eccsize = major_count * minor_count;
                UInt32 major, minor;
                for (major = 0; major < major_count; major++)
                {
                    var index = (major >> 1) * major_mult + (major & 1);
                    byte ecc_a = 0;
                    byte ecc_b = 0;
                    for (minor = 0; minor < minor_count; minor++)
                    {
                        byte temp = sectorECCP[index];
                        index += minor_inc;
                        if (index >= eccsize) index -= eccsize;
                        ecc_a ^= temp;
                        ecc_b ^= temp;
                        ecc_a = ecc_f_lut[ecc_a];
                    }
                    ecc_a = ecc_b_lut[ecc_f_lut[ecc_a] ^ ecc_b];
                    dest[0 + major] = ecc_a;
                    dest[0 + major + major_count] = (byte)(ecc_a ^ ecc_b);
                }

                File.WriteAllBytes("ECCP.RAW", dest);


                major_count = 52;
                minor_count = 43;
                major_mult = 86;
                minor_inc = 88;

                //
                eccsize = major_count * minor_count;
                //UInt32 major, minor;
                for (major = 0; major < major_count; major++)
                {
                    var index = (major >> 1) * major_mult + (major & 1);
                    byte ecc_a = 0;
                    byte ecc_b = 0;
                    for (minor = 0; minor < minor_count; minor++)
                    {
                        byte temp = sectorECCQ[index];
                        index += minor_inc;
                        if (index >= eccsize) index -= eccsize;
                        ecc_a ^= temp;
                        ecc_b ^= temp;
                        ecc_a = ecc_f_lut[ecc_a];
                    }
                    ecc_a = ecc_b_lut[ecc_f_lut[ecc_a] ^ ecc_b];
                    dest[172 + major] = ecc_a;
                    dest[172 + major + major_count] = (byte)(ecc_a ^ ecc_b);
                }

                File.WriteAllBytes("ECCQ.RAW", dest);


                //
                var type = size == 2332 ? "Mode 2 Form 2" : "Mode 2 Form 1";
                LOG.Add(string.Format("0x{0:x8} - 0x{1:x8} - {2}", edc, innerEDC, type));
                //MessageBox.Show(string.Format("0x{0:x8} - 0x{1:x8}", edc, innerEDC));

                cursor += 2352;
            }
            File.WriteAllLines("LOG.txt", LOG);
        }

        private object computeEDC(byte[] sector)
        {
            UInt32 edc = 0;
            var count = sector.Length;
            var i = 0;
            while (count-- != 0)
            {
                edc = (UInt32)((edc >> 8) ^ edc_lut[(edc ^ (sector[i++])) & 0xFF]);
            }
            return edc;
        }

        private void textBox1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            var file = (string[])e.Data.GetData(DataFormats.FileDrop);
            textBox1.Text = file[0];
        }
    }
}
