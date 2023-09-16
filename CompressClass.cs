using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using zlib;

namespace UPK_Environment
{
    internal class CompressClass
    {
        public static int pad(int size, int padder)
        {
            if(size % padder != 0)
            {
                while(size % padder != 0)
                {
                    size++;
                }
            }

            return size;
        }

        private static void cmd(string exe, string txt_path, string file_name, string compressed)
        {

            Process proc = Process.Start(new ProcessStartInfo(exe, "\"" + txt_path + "\" \"" + file_name + "\" \"" + compressed + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            proc.OutputDataReceived += (DataReceivedEventHandler)((sender, e) => Console.WriteLine(e.Data));
            proc.BeginOutputReadLine();

            proc.WaitForExit();
            if (proc.ExitCode != 0)
                Console.WriteLine("ExitCode: {0}", (object)proc.ExitCode);
            proc.Close();
        }

        private static void CompressData(byte[] inData, out byte[] outData)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
            using (Stream inMemoryStream = new MemoryStream(inData))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                outData = outMemoryStream.ToArray();
            }
        }

        private static void DecompressData(byte[] inData, out byte[] outData)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream))
            using (Stream inMemoryStream = new MemoryStream(inData))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                outData = outMemoryStream.ToArray();
            }
        }

        private static void CopyStream(System.IO.Stream input, System.IO.Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }
            output.Flush();
        }

        public static bool Compress(string path, string compress_path, string tmp_path, string tool_path, int flags, int head_off, int file_off, int export_count, int export_off)
        {
            if (!Directory.Exists(tmp_path)) Directory.CreateDirectory(tmp_path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            FileStream fs = new FileStream(path, FileMode.Open);
            byte[] part1 = new byte[113];
            byte[] part2 = new byte[head_off - 4 - 113];
            fs.Read(part1, 0, part1.Length);
            fs.Seek(117, SeekOrigin.Begin);
            fs.Read(part2, 0, part2.Length);

            List<int> blocks = new List<int>();
            int offset = export_off;
            int size = file_off - head_off;
            int def_sz = 0x100000;

            byte[] tmp;

            for (int i = 0; i < export_count; i++)
            {
                offset += 32;
                tmp = new byte[4];
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Read(tmp, 0, tmp.Length);
                int file_sz = BitConverter.ToInt32(tmp, 0);

                if (def_sz > size + file_sz && (i == export_count - 1))
                {
                    def_sz = size + file_sz;
                    blocks.Add(def_sz);
                }

                if (def_sz == 0x100000)
                {
                    if (size + file_sz >= def_sz)
                    {
                        if(size == 0)
                        {
                            size = file_sz;
                        }

                        blocks.Add(size);
                        size = 0;
                        def_sz = 0x100000;
                    }
                }

                size += file_sz;
                offset += 12;

                fs.Seek(offset, SeekOrigin.Begin);
                tmp = new byte[4];
                fs.Read(tmp, 0, tmp.Length);

                if (BitConverter.ToInt32(tmp, 0) == 1) offset += 4;

                offset += 24;
            }

            int block_sz = (16 * blocks.Count) + 4;
            byte[] header = new byte[block_sz];
            tmp = new byte[4];
            tmp = BitConverter.GetBytes((int)blocks.Count);
            Array.Copy(tmp, 0, header, 0, tmp.Length);

            int c_off = head_off + block_sz - 4;
            int off = head_off;
            //int bl_sz = 0, c_bl_sz = 0;

            int h_off = 4;

            offset = head_off;
            fs.Seek(offset, SeekOrigin.Begin);

            def_sz = 0x20000;

            byte[] head = { 0xC1, 0x83, 0x2A, 0x9E };

            byte[] sub_block_sz = BitConverter.GetBytes(def_sz);


            for(int i = 0; i < blocks.Count; i++)
            {
                int count = pad(blocks[i], def_sz) / def_sz;
                int subhead_sz = 16 + (8 * count);

                int c_size = 0;

                byte[] sub_header = new byte[subhead_sz];

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(off);

                Array.Copy(tmp, 0, header, h_off, tmp.Length);

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_off);

                Array.Copy(tmp, 0, header, h_off + 8, tmp.Length);

                Array.Copy(head, 0, sub_header, 0, head.Length);
                Array.Copy(sub_block_sz, 0, sub_header, 4, sub_block_sz.Length);

                int sub_offset = 0;
                int sub_head_offset = 12;

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(blocks[i]);
                Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                int sub_bl_sz = def_sz;

                sub_head_offset += 4;

                for (int j = 0; j < count; j++)
                {
                    sub_bl_sz = def_sz;

                    if (sub_bl_sz > blocks[i] - sub_offset) sub_bl_sz = blocks[i] - sub_offset;

                    byte[] sub_bl = new byte[sub_bl_sz];
                    fs.Read(sub_bl, 0, sub_bl.Length);

                    if (File.Exists(tmp_path + "\\" + i + "_" + j + ".block")) File.Delete(tmp_path + "\\" + i + "_" + j + ".block");

                    FileStream fw = new FileStream(tmp_path + "\\" + i + "_" + j + ".block", FileMode.CreateNew);
                    fw.Write(sub_bl, 0, sub_bl.Length);
                    fw.Close();

                    cmd(tool_path, compress_path, tmp_path + "\\" + i + "_" + j + ".block", tmp_path);

                    File.Delete(tmp_path + "\\" + i + "_" + j + ".block");

                    sub_bl = File.ReadAllBytes(tmp_path + "\\" + i + "_" + j + "_pack.dat");

                    c_size += sub_bl.Length;

                    tmp = new byte[4];
                    tmp = BitConverter.GetBytes(sub_bl.Length);
                    Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                    sub_head_offset += 4;

                    //bat_str += "\"" + tool_path + "\" \"" + compress_path + "\" \"" + tmp_path + "\\" + i + "_" + j + ".block\" \"" + tmp_path + "\"\r\n";
                    //bat_str += "del \"" + tmp_path + "\\" + i + "_" + j + ".block\"\r\n";

                    sub_offset += sub_bl_sz;

                    tmp = new byte[4];
                    tmp = BitConverter.GetBytes(sub_bl_sz);

                    Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                    if (j < count - 1)
                        sub_head_offset += 4;
                }

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_size);
                Array.Copy(tmp, 0, sub_header, 8, tmp.Length);

                if (File.Exists(tmp_path + "\\" + i + ".head")) File.Delete(tmp_path + "\\" + i + ".head");
                FileStream fh = new FileStream(tmp_path + "\\" + i + ".head", FileMode.CreateNew);
                fh.Write(sub_header, 0, sub_header.Length);
                fh.Close();

                c_size += subhead_sz;

                off += blocks[i];

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(blocks[i]);

                Array.Copy(tmp, 0, header, h_off + 4, tmp.Length);

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_size);

                Array.Copy(tmp, 0, header, h_off + 12, tmp.Length);

                c_off += c_size;
                offset += blocks[i];
                h_off += 16;
            }

            //System.Windows.MessageBox.Show(blocks.Count.ToString());

            fs.Close();

            fs = new FileStream(path + ".tmp", FileMode.CreateNew);

            tmp = new byte[4];
            tmp = BitConverter.GetBytes(flags);
            Array.Copy(tmp, 0, part1, 21, tmp.Length);

            tmp = new byte[4];
            tmp = BitConverter.GetBytes((int)2);
            Array.Copy(tmp, 0, part1, 109, tmp.Length);

            fs.Write(part1, 0, part1.Length);
            fs.Write(header, 0, header.Length);
            fs.Write(part2, 0, part2.Length);

            for(int i = 0; i < blocks.Count; i++)
            {
                int count = pad(blocks[i], 0x20000) / 0x20000;

                header = new byte[0];
                header = File.ReadAllBytes(tmp_path + "\\" + i + ".head");

                fs.Write(header, 0, header.Length);

                File.Delete(tmp_path + "\\" + i + ".head");

                for(int j = 0; j < count; j++)
                {
                    byte[] sub_block = File.ReadAllBytes(tmp_path + "\\" + i + "_" + j + "_pack.dat");
                    fs.Write(sub_block, 0, sub_block.Length);
                    sub_block = null;
                    File.Delete(tmp_path + "\\" + i + "_" + j + "_pack.dat");
                }
            }

            header = null;
            blocks.Clear();

            fs.Close();

            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);

            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");

            if (Directory.Exists(tmp_path)) Directory.Delete(tmp_path);

            return true;
        }

        public static int decompress_zlib(string archive_path, string tmp_archive, int archive_format)
        {
            try
            {
                if (archive_format == 1)
                {
                    if (File.Exists(tmp_archive)) File.Delete(tmp_archive);
                    FileStream new_fs = new FileStream(tmp_archive, FileMode.CreateNew);
                    FileStream fs = new FileStream(archive_path, FileMode.Open);
                    BinaryReader br = new BinaryReader(fs);
                    byte[] header = br.ReadBytes(4);
                    int chunk = br.ReadInt32();
                    int compressed_sz = br.ReadInt32();
                    int size = br.ReadInt32();

                    int count = pad(size, chunk) / chunk;

                    int c_off = 16;
                    int bl_off = 16 + (count * 8);

                    uint c_size = 0, bl_size = 0;

                    byte[] zlib_compress, block;

                    for(int i = 0; i < count; i++)
                    {
                        br.BaseStream.Seek(c_off, SeekOrigin.Begin);
                        c_size = br.ReadUInt32();
                        bl_size = br.ReadUInt32();
                        c_off += 8;

                        br.BaseStream.Seek(bl_off, SeekOrigin.Begin);
                        zlib_compress = br.ReadBytes((int)c_size);
                        bl_off += (int)c_size;
                        block = new byte[bl_size];

                        DecompressData(zlib_compress, out block);

                        new_fs.Write(block, 0, block.Length);
                        zlib_compress = null;
                        block = null;
                    }

                    new_fs.Close();
                    br.Close();
                    fs.Close();
                }
                else
                {

                }

                return 1;
            }
            catch
            {
                return -1;
            }
        }

        public static bool CompressNew(string path, int flags, int head_off, int file_off, int export_count, int export_off, int com_off)
        {
            //if (!Directory.Exists(tmp_path)) Directory.CreateDirectory(tmp_path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            FileStream fs = new FileStream(path, FileMode.Open);
            byte[] part1 = new byte[com_off + 4];
            byte[] part2 = new byte[head_off - 4 - (com_off + 4)];
            fs.Read(part1, 0, part1.Length);
            fs.Seek(com_off, SeekOrigin.Begin);
            fs.Read(part2, 0, part2.Length);

            List<int> blocks = new List<int>();
            int offset = export_off;
            int size = file_off - head_off;
            int def_sz = 0x100000;

            byte[] tmp;

            for (int i = 0; i < export_count; i++)
            {
                offset += 32;
                tmp = new byte[4];
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Read(tmp, 0, tmp.Length);
                int file_sz = BitConverter.ToInt32(tmp, 0);

                if (def_sz > size + file_sz && (i == export_count - 1))
                {
                    def_sz = size + file_sz;
                    blocks.Add(def_sz);
                }

                if (def_sz == 0x100000)
                {
                    if (size + file_sz >= def_sz)
                    {
                        if (size == 0)
                        {
                            size = file_sz;
                        }

                        blocks.Add(size);
                        size = 0;
                        def_sz = 0x100000;
                    }
                }

                size += file_sz;
                offset += 12;

                fs.Seek(offset, SeekOrigin.Begin);
                tmp = new byte[4];
                fs.Read(tmp, 0, tmp.Length);

                if (BitConverter.ToInt32(tmp, 0) == 1) offset += 4;

                offset += 24;
            }

            int block_sz = (16 * blocks.Count) + 4;
            byte[] header = new byte[block_sz];
            tmp = new byte[4];
            tmp = BitConverter.GetBytes((int)blocks.Count);
            Array.Copy(tmp, 0, header, 0, tmp.Length);

            int c_off = head_off + block_sz - 4;
            int off = head_off;
            //int bl_sz = 0, c_bl_sz = 0;

            int h_off = 4;

            offset = head_off;
            fs.Seek(offset, SeekOrigin.Begin);

            def_sz = 0x20000;

            byte[] head = { 0xC1, 0x83, 0x2A, 0x9E };

            byte[] sub_block_sz = BitConverter.GetBytes(def_sz);

            tmp = new byte[4];
            tmp = BitConverter.GetBytes(flags);
            Array.Copy(tmp, 0, part1, 21, tmp.Length);

            tmp = new byte[4];
            tmp = BitConverter.GetBytes((int)2);
            Array.Copy(tmp, 0, part1, com_off, tmp.Length);

            FileStream new_fs = new FileStream(path + ".tmp", FileMode.CreateNew);

            new_fs.Write(part1, 0, part1.Length);
            new_fs.Write(header, 0, header.Length);
            new_fs.Write(part2, 0, part2.Length);
            int f_head_off = part1.Length;
            int f_c_off = part1.Length + header.Length + part2.Length;

            byte[] _workMemory = new byte[16384L * 4];
            byte[] c_block;
            int c_bl_len;

            for (int i = 0; i < blocks.Count; i++)
            {
                int count = pad(blocks[i], def_sz) / def_sz;
                int subhead_sz = 16 + (8 * count);

                int c_size = 0;

                byte[] sub_header = new byte[subhead_sz];

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(off);

                Array.Copy(tmp, 0, header, h_off, tmp.Length);

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_off);

                Array.Copy(tmp, 0, header, h_off + 8, tmp.Length);

                Array.Copy(head, 0, sub_header, 0, head.Length);
                Array.Copy(sub_block_sz, 0, sub_header, 4, sub_block_sz.Length);

                int sub_offset = 0;
                int sub_head_offset = 12;

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(blocks[i]);
                Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                int sub_bl_sz = def_sz;

                sub_head_offset += 4;

                MemoryStream ms = new MemoryStream();

                ms.Write(sub_header, 0, sub_header.Length);

                for (int j = 0; j < count; j++)
                {
                    sub_bl_sz = def_sz;

                    if (sub_bl_sz > blocks[i] - sub_offset) sub_bl_sz = blocks[i] - sub_offset;

                    byte[] sub_bl = new byte[sub_bl_sz];
                    fs.Read(sub_bl, 0, sub_bl.Length);

                    c_bl_len = 0;

                    c_block = new byte[sub_bl.Length + sub_bl.Length / 64 + 16 + 3 + 4];
                    UPKpacker.lzo1x_1_compress(sub_bl, sub_bl.Length, c_block, ref c_bl_len, _workMemory);
                    sub_bl = new byte[c_bl_len];
                    Array.Copy(c_block, 0, sub_bl, 0, sub_bl.Length);

                    ms.Write(sub_bl, 0, sub_bl.Length);

                    c_block = null;
                    c_size += sub_bl.Length;

                    tmp = new byte[4];
                    tmp = BitConverter.GetBytes(sub_bl.Length);
                    Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                    sub_head_offset += 4;

                    //bat_str += "\"" + tool_path + "\" \"" + compress_path + "\" \"" + tmp_path + "\\" + i + "_" + j + ".block\" \"" + tmp_path + "\"\r\n";
                    //bat_str += "del \"" + tmp_path + "\\" + i + "_" + j + ".block\"\r\n";

                    sub_offset += sub_bl_sz;

                    tmp = new byte[4];
                    tmp = BitConverter.GetBytes(sub_bl_sz);

                    Array.Copy(tmp, 0, sub_header, sub_head_offset, tmp.Length);

                    if (j < count - 1)
                        sub_head_offset += 4;
                }

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_size);
                Array.Copy(tmp, 0, sub_header, 8, tmp.Length);
                tmp = ms.ToArray();

                Array.Copy(sub_header, 0, tmp, 0, sub_header.Length);

                ms.Close();

                new_fs.Write(tmp, 0, tmp.Length);

                c_size += subhead_sz;

                off += blocks[i];

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(blocks[i]);

                Array.Copy(tmp, 0, header, h_off + 4, tmp.Length);

                tmp = new byte[4];
                tmp = BitConverter.GetBytes(c_size);

                Array.Copy(tmp, 0, header, h_off + 12, tmp.Length);

                c_off += c_size;
                offset += blocks[i];
                h_off += 16;

                sub_header = null;
            }

            //System.Windows.MessageBox.Show(blocks.Count.ToString());

            fs.Close();

            new_fs.Seek(f_head_off, SeekOrigin.Begin);
            new_fs.Write(header, 0, header.Length);
            //fs = new FileStream(path + ".tmp", FileMode.CreateNew);

            
            header = null;
            tmp = null;
            sub_block_sz = null;
            blocks.Clear();

            fs.Close();
            new_fs.Close();

            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);

            //if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");

            //if (Directory.Exists(tmp_path)) Directory.Delete(tmp_path);

            return true;
        }
    }
}
