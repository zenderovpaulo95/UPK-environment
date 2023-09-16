using System;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using WinForms = System.Windows.Forms.FolderBrowserDialog;
using System.Windows.Documents;

namespace UPK_Environment
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        string[] formats = { ".umap", ".u", ".upk", ".xxx" };
        string sel_format = ".umap";
        int archive_format = 0; //Сжатые блоки, 1 - архив весь сжат
        int compress_type = 0; //LZO по умолчанию

        private bool is_compress(string path, ref int archive_format, ref int com_type, int com_offset)
        {
            try
            {
                compress_type = 0;
                FileStream fs = new FileStream(path, FileMode.Open);
                BinaryReader br = new BinaryReader(fs);
                br.BaseStream.Seek(4, SeekOrigin.Begin);
                int check = br.ReadInt32();
                int offset = 0;
                byte[] tmp;

                if(check == 0x20000)
                {
                    br.BaseStream.Seek(8, SeekOrigin.Begin);
                    int size;
                    check = br.ReadInt32();
                    size = br.ReadInt32();
                    int count = CompressClass.pad(size, 0x20000) / 0x20000;
                    int check_c_size = (int)fs.Length - (16 + (count * 8));

                    offset = 16 + (count * 8);
                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    tmp = br.ReadBytes(2);

                    if (System.Text.Encoding.ASCII.GetString(tmp) == "\x78\x9C" || System.Text.Encoding.ASCII.GetString(tmp) == "\x78\xDA") com_type = 1; //zlib

                    br.Close();
                    fs.Close();

                    archive_format = 1;
                    com_type = 1;

                    return check_c_size == check;
                }
                br.BaseStream.Seek(com_offset, SeekOrigin.Begin);
                int type = br.ReadInt32();

                br.Close();
                fs.Close();

                return type == 2;
            }
            catch
            {
                return false;
            }
        }

        private string GetFileName(string path)
        {
            string format = null;

            for (int i = 0; i < formats.Length; i++)
                if (path.ToLower().IndexOf(formats[i]) > 0)
                {
                    format = formats[i];
                    sel_format = formats[i];
                }


            if (format != null)
            {
                string result = path.Substring(0, path.ToLower().IndexOf(format));
                for (int i = result.Length - 1; i >= 0; i--)
                {
                    if (result[i] == '\\')
                    {
                        result = result.Substring(i + 1, result.Length - (i + 1));
                        break;
                    }
                }

                return result;
            }
            else return null;
        }

        private int GetComOffset(int sel_ind)
        {
            int[] compress_offset = new int[2];
            compress_offset[0] = 109;
            compress_offset[1] = 97;

            if (sel_ind > compress_offset.Length) return -1;

            return compress_offset[sel_ind];
        }

        private void Unpack_Btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Выберите архив от Unreal Engine 3";

            if(ofd.ShowDialog() == true)
            {
                try
                {
                    string out_dir = AppDomain.CurrentDomain.BaseDirectory + "\\Unpacked";
                    if (!Directory.Exists(out_dir))
                        Directory.CreateDirectory(out_dir);

                    string extr_files_log = AppDomain.CurrentDomain.BaseDirectory + "\\log.txt";

                    if (File.Exists(extr_files_log)) File.Delete(extr_files_log);

                    string archive = ofd.FileName;

                    int com_off = GetComOffset(GamesSelect.SelectedIndex);

                    if (com_off != -1 && is_compress(archive, ref archive_format, ref compress_type, com_off) && compress_type == 1)
                    {
                        archive += ".tmp";
                        if (CompressClass.decompress_zlib(ofd.FileName, archive, archive_format) == -1) throw new Exception("Неизвестная ошибка. Пришли архив для изучения");
                    }

                    UPKpacker.UPKunpack(archive, out_dir, true, extr_files_log);
                    if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();

                    if (archive.IndexOf(".tmp") == archive.Length - 4)
                    {
                        File.Delete(archive);
                        string filename = GetFileName(archive);
                        Directory.Move(out_dir + "\\" + filename + sel_format, out_dir + "\\" + filename);
                    }

                    string log = null;

                    if (File.Exists(extr_files_log))
                    {
                        log = File.ReadAllText(extr_files_log);
                        File.Delete(extr_files_log);
                    }

                    if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));
                    else MessageBox.Show("Если не написался отчёт о распаковке, сообщи мне в ЛС (pashok6798).", "Странно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {

                }
            }
        }

        private void Pack_Compress_Btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            
            ofd.Title = "Выберите архив от Unreal Engine 3 для пересборки";

            if(ofd.ShowDialog() == true)
            {
                if (File.Exists(ofd.FileName))
                {
                    int check_size = 0;

                    string FileName = GetFileName(ofd.FileName);

                    if (FileName != null)
                    {
                        string DefPath = AppDomain.CurrentDomain.BaseDirectory + "Unpacked\\" + FileName;

                        string LogFile = AppDomain.CurrentDomain.BaseDirectory + "\\log.txt";

                        if (File.Exists(LogFile)) File.Delete(LogFile);

                        string log = null;

                        int com_offset = GetComOffset(GamesSelect.SelectedIndex);

                        if (Directory.Exists(DefPath))
                        {
                            if (com_offset != -1 && !is_compress(ofd.FileName, ref archive_format, ref compress_type, com_offset))
                            {
                                MessageBox.Show("Архив не был сжат, поэтому я просто пересоберу архив...", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                                UPKpacker.UPKrepack(DefPath, ofd.FileName, true, LogFile);

                                if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();

                                if (File.Exists(LogFile))
                                {
                                    log = File.ReadAllText(LogFile);
                                    File.Delete(LogFile);
                                }

                                if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));
                            }
                            else
                            {
                                int file_off = 0, head_off = 0;
                                int flags = -1, export_count = 0, export_off = 0;

                                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "dontshowagain.txt"))
                                {
                                    var result = MessageBox.Show("Большие сложные по содержимому архивы будут долго сжиматься. Придётся потерпеть какое-то время", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    if (result == MessageBoxResult.OK)
                                    {
                                        string showed = "True";
                                        File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "dontshowagain.txt", showed);
                                    }
                                }

                                FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
                                BinaryReader br = new BinaryReader(fs);

                                br.BaseStream.Seek(8, SeekOrigin.Begin);
                                file_off = br.ReadInt32();

                                br.BaseStream.Seek(21, SeekOrigin.Begin);
                                flags = br.ReadInt32();

                                br.BaseStream.Seek(29, SeekOrigin.Begin);
                                head_off = br.ReadInt32();
                                export_count = br.ReadInt32();
                                export_off = br.ReadInt32();

                                br.Close();
                                fs.Close();

                                UPKpacker.UPKrepack(DefPath, ofd.FileName, true, LogFile);

                                if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();

                                if (File.Exists(LogFile))
                                {
                                    log = File.ReadAllText(LogFile);
                                    File.Delete(LogFile);
                                }

                                if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));

                                /*string compress_str = "comtype lzo1x_compress\r\nget SIZE asize\r\nget NAME basename\r\nget EXT extension\r\nstring NAME + \"_pack.dat\"";
                                compress_str += "\r\nclog NAME 0 SIZE SIZE";

                                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "compress.txt"))
                                {
                                    fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "compress.txt", FileMode.CreateNew);
                                    StreamWriter sw = new StreamWriter(fs);
                                    sw.Write(compress_str);
                                    sw.Close();
                                    fs.Close();
                                }*/

                                //CompressClass.Compress(ofd.FileName, AppDomain.CurrentDomain.BaseDirectory + "compress.txt", AppDomain.CurrentDomain.BaseDirectory + "tmp", AppDomain.CurrentDomain.BaseDirectory + "quickbms.exe", flags, head_off, file_off, export_count, export_off);
                                int com_off = GetComOffset(GamesSelect.SelectedIndex);

                                if(com_off != -1 && compress_type == 0) CompressClass.CompressNew(ofd.FileName, flags, head_off, file_off, export_count, export_off, com_off);
                                else
                                {
                                    fs = new FileStream(ofd.FileName, FileMode.Open);
                                    if (File.Exists(ofd.FileName + ".tmp")) File.Delete(ofd.FileName + ".tmp");
                                    FileStream new_fs = new FileStream(ofd.FileName + ".tmp", FileMode.CreateNew);
                                    int chunk_sz = 0x20000;
                                    byte[] tmp = new byte[4];
                                    int archive_sz = (int)fs.Length;

                                    int count = CompressClass.pad(archive_sz, 0x20000) / 0x20000;

                                    int offset = 0;

                                    byte[] header = { 0xC1, 0x83, 0x2A, 0x9E };

                                    MemoryStream ms = new MemoryStream();
                                    new_fs.Write(header, 0, header.Length);
                                    tmp = BitConverter.GetBytes(chunk_sz);
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    tmp = new byte[4];
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    tmp = BitConverter.GetBytes(archive_sz);
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    tmp = new byte[count * 8];
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    int common_c_size = 0;
                                    int c_size = 0;
                                    byte[] c_tmp;

                                    byte[] _workMemory = new byte[16384L * 4];



                                    for (int j = 0; j < count; j++)
                                    {
                                        if (archive_sz - offset < chunk_sz) chunk_sz = archive_sz - offset;

                                        tmp = new byte[chunk_sz];
                                        fs.Read(tmp, 0, tmp.Length);
                                        c_tmp = new byte[tmp.Length + tmp.Length / 64 + 16 + 3 + 4];
                                        UPKpacker.lzo1x_1_compress(tmp, chunk_sz, c_tmp, ref c_size, _workMemory);
                                        tmp = new byte[c_size];
                                        Array.Copy(c_tmp, 0, tmp, 0, c_size);
                                        c_tmp = null;
                                        new_fs.Write(tmp, 0, tmp.Length);

                                        c_tmp = new byte[4];
                                        c_tmp = BitConverter.GetBytes(c_size);
                                        ms.Write(c_tmp, 0, c_tmp.Length);
                                        c_tmp = new byte[4];
                                        c_tmp = BitConverter.GetBytes(chunk_sz);
                                        ms.Write(c_tmp, 0, c_tmp.Length);

                                        common_c_size += c_size;
                                        offset += chunk_sz;
                                        c_tmp = null;
                                    }

                                    tmp = ms.ToArray();

                                    ms.Close();
                                    fs.Close();

                                    new_fs.Seek(16, SeekOrigin.Begin);
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    tmp = new byte[4];
                                    tmp = BitConverter.GetBytes(common_c_size);
                                    new_fs.Seek(8, SeekOrigin.Begin);
                                    new_fs.Write(tmp, 0, tmp.Length);
                                    new_fs.Close();

                                    if (File.Exists(ofd.FileName)) File.Delete(ofd.FileName);

                                    File.Move(ofd.FileName + ".tmp", ofd.FileName);
                                    string txt = ofd.FileName + ".uncompressed_size";
                                    File.WriteAllText(txt, Convert.ToString(archive_sz));
                                }

                                MessageBox.Show("Вроде собрал и сжал. Если что-то будет не так, пиши мне и скинь нужный тебе архив", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else MessageBox.Show("Убедись, что папка с распакованными файлами расположена в папке \"Unpacked\"", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else MessageBox.Show("Возможно, формат не поддерживается. Добавь в текстовый файл \"formats.txt\" нужный тебе формат и сохрани этот файл рядом с оболочкой.");
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogTxtBox.Document.Blocks.Clear();
            LogTxtBox.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\UPKPacker.dll")
                || !File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\lzo2.dll"))
            {
                var result = MessageBox.Show("Проверь на наличие файлов UPKPacker.dll и lzo2.dll рядом с оболочкой.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                if (result == MessageBoxResult.OK) Application.Current.MainWindow.Close();
            }

            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\formats.txt")) formats = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "\\formats.txt");
            GamesSelect.Items.Add("По умолчанию (UE3. Какие-то архивы сжимаются и работают без проблем)");
            GamesSelect.SelectedIndex = 0;
            GamesSelect.Items.Add("Borderlands (Костыль для правильного сжатия)");
        }

        private void PackSeveralArchivesBtn_Click(object sender, RoutedEventArgs e)
        {
            //OpenFileDialog ofd = new OpenFileDialog();
            var fbd = new WinForms();
            //ofd.Title = "Выберите архив от Unreal Engine 3 для пересборки";

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (Directory.Exists(fbd.SelectedPath))
                {
                    DirectoryInfo di = new DirectoryInfo(fbd.SelectedPath);
                    for(int j = 0; j < formats.Length; j++)
                    { 
                    FileInfo[] fi = di.GetFiles("*" + formats[j], SearchOption.AllDirectories); //Для xxx форматов

                        if (fi.Length > 0)
                        {
                            if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();
                            pb.Minimum = 0;
                            pb.Maximum = fi.Length - 1;

                            for (int i = 0; i < fi.Length; i++)
                            {
                                string FileName = GetFileName(fi[i].Name);
                                if (FileName != null)
                                {

                                    string DefPath = AppDomain.CurrentDomain.BaseDirectory + "Unpacked\\" + FileName;

                                    string LogFile = AppDomain.CurrentDomain.BaseDirectory + "\\log.txt";

                                    if (File.Exists(LogFile)) File.Delete(LogFile);

                                    string log = null;

                                    if (Directory.Exists(DefPath))
                                    {
                                        int com_off = GetComOffset(GamesSelect.SelectedIndex);

                                        if (com_off != -1 && !is_compress(fi[i].FullName, ref archive_format, ref compress_type, com_off))
                                        {
                                            //MessageBox.Show("Архив не был сжат, поэтому я просто пересоберу архив...", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                                            UPKpacker.UPKrepack(DefPath, fi[i].FullName, true, LogFile);

                                            //if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();

                                            if (File.Exists(LogFile))
                                            {
                                                log = File.ReadAllText(LogFile);
                                                File.Delete(LogFile);
                                            }

                                            //if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));
                                            LogTxtBox.Document.Blocks.Add(new Paragraph(new Run("Файл " + fi[i].Name + " успешно пересобран")));
                                            //pb.Value = i;
                                        }
                                        else
                                        {
                                            int file_off = 0, head_off = 0;
                                            int flags = -1, export_count = 0, export_off = 0;

                                            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "dontshowagain.txt"))
                                            {
                                                var result = MessageBox.Show("Большие сложные по содержимому архивы будут долго сжиматься. Придётся потерпеть какое-то время", "Внимание!", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                if (result == MessageBoxResult.OK)
                                                {
                                                    string showed = "True";
                                                    File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "dontshowagain.txt", showed);
                                                }
                                            }

                                            FileStream fs = new FileStream(fi[i].FullName, FileMode.Open);
                                            BinaryReader br = new BinaryReader(fs);

                                            br.BaseStream.Seek(8, SeekOrigin.Begin);
                                            file_off = br.ReadInt32();

                                            br.BaseStream.Seek(21, SeekOrigin.Begin);
                                            flags = br.ReadInt32();

                                            br.BaseStream.Seek(29, SeekOrigin.Begin);
                                            head_off = br.ReadInt32();
                                            export_count = br.ReadInt32();
                                            export_off = br.ReadInt32();

                                            br.Close();
                                            fs.Close();

                                            UPKpacker.UPKrepack(DefPath, fi[i].FullName, true, LogFile);

                                            if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();

                                            if (File.Exists(LogFile))
                                            {
                                                log = File.ReadAllText(LogFile);
                                                File.Delete(LogFile);
                                            }

                                            if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));

                                            /*string compress_str = "comtype lzo1x_compress\r\nget SIZE asize\r\nget NAME basename\r\nget EXT extension\r\nstring NAME + \"_pack.dat\"";
                                            compress_str += "\r\nclog NAME 0 SIZE SIZE";

                                            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "compress.txt"))
                                            {
                                                fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "compress.txt", FileMode.CreateNew);
                                                StreamWriter sw = new StreamWriter(fs);
                                                sw.Write(compress_str);
                                                sw.Close();
                                                fs.Close();
                                            }*/

                                            //CompressClass.Compress(fi[i].FullName, AppDomain.CurrentDomain.BaseDirectory + "compress.txt", AppDomain.CurrentDomain.BaseDirectory + "tmp", AppDomain.CurrentDomain.BaseDirectory + "quickbms.exe", flags, head_off, file_off, export_count, export_off);

                                            //int com_off = GetComOffset(GamesSelect.SelectedIndex);

                                            if(com_off != -1 && compress_type == 0) CompressClass.CompressNew(fi[i].FullName, flags, head_off, file_off, export_count, export_off, com_off);
                                            else
                                            {
                                                fs = new FileStream(fi[i].FullName, FileMode.Open);
                                                if (File.Exists(fi[i].FullName + ".tmp")) File.Delete(fi[i].FullName + ".tmp");
                                                FileStream new_fs = new FileStream(fi[i].FullName + ".tmp", FileMode.CreateNew);
                                                int chunk_sz = 0x20000;
                                                byte[] tmp = new byte[4];
                                                int archive_sz = (int)fs.Length;

                                                int count = CompressClass.pad(archive_sz, 0x20000) / 0x20000;

                                                int offset = 0;

                                                byte[] header = { 0xC1, 0x83, 0x2A, 0x9E };

                                                MemoryStream ms = new MemoryStream();
                                                new_fs.Write(header, 0, header.Length);
                                                tmp = BitConverter.GetBytes(chunk_sz);
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                tmp = new byte[4];
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                tmp = BitConverter.GetBytes(archive_sz);
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                tmp = new byte[count * 8];
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                int common_c_size = 0;
                                                int c_size = 0;
                                                byte[] c_tmp;

                                                byte[] _workMemory = new byte[16384L * 4];



                                                for (int k = 0; k < count; k++)
                                                {
                                                    if (archive_sz - offset < chunk_sz) chunk_sz = archive_sz - offset;

                                                    tmp = new byte[chunk_sz];
                                                    fs.Read(tmp, 0, tmp.Length);
                                                    c_tmp = new byte[tmp.Length + tmp.Length / 64 + 16 + 3 + 4];
                                                    UPKpacker.lzo1x_1_compress(tmp, chunk_sz, c_tmp, ref c_size, _workMemory);
                                                    tmp = new byte[c_size];
                                                    Array.Copy(c_tmp, 0, tmp, 0, c_size);
                                                    c_tmp = null;
                                                    new_fs.Write(tmp, 0, tmp.Length);

                                                    c_tmp = new byte[4];
                                                    c_tmp = BitConverter.GetBytes(c_size);
                                                    ms.Write(c_tmp, 0, c_tmp.Length);
                                                    c_tmp = new byte[4];
                                                    c_tmp = BitConverter.GetBytes(chunk_sz);
                                                    ms.Write(c_tmp, 0, c_tmp.Length);

                                                    common_c_size += c_size;
                                                    offset += chunk_sz;
                                                    c_tmp = null;
                                                }

                                                tmp = ms.ToArray();

                                                ms.Close();
                                                fs.Close();

                                                new_fs.Seek(16, SeekOrigin.Begin);
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                tmp = new byte[4];
                                                tmp = BitConverter.GetBytes(common_c_size);
                                                new_fs.Seek(8, SeekOrigin.Begin);
                                                new_fs.Write(tmp, 0, tmp.Length);
                                                new_fs.Close();

                                                if (File.Exists(fi[i].FullName)) File.Delete(fi[i].FullName);

                                                File.Move(fi[i].FullName + ".tmp", fi[i].FullName);
                                                string txt = fi[i].FullName + ".uncompressed_size";
                                                File.WriteAllText(txt, Convert.ToString(archive_sz));
                                            }

                                            //MessageBox.Show("Вроде собрал и сжал. Если что-то будет не так, пиши мне и скинь нужный тебе архив", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                                        }
                                    }
                                    else LogTxtBox.Document.Blocks.Add(new Paragraph(new Run("Убедись, что папка с распакованными файлами расположена в папке \"Unpacked\"")));//MessageBox.Show("Убедись, что папка с распакованными файлами расположена в папке \"Unpacked\"", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    pb.Value = i;
                                }
                                else
                                {
                                    MessageBox.Show("Возможно, формат не поддерживается. Добавь в текстовый файл \"formats.txt\" нужный тебе формат и сохрани этот файл рядом с оболочкой.");
                                    break;
                                }
                            }
                        }
                    }
                }

            }
        }

        private void ExtractSeveralArchivesBtn_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new WinForms();

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = fbd.SelectedPath;

                if (Directory.Exists(path))
                {
                    DirectoryInfo di = new DirectoryInfo(path);
                    for (int j = 0; j < formats.Length; j++)
                    {
                        FileInfo[] fi = di.GetFiles("*" + formats[j], SearchOption.AllDirectories); //Для xxx форматов

                        if (fi.Length > 0)
                        {
                            if (LogTxtBox.Document.Blocks.Count > 0) LogTxtBox.Document.Blocks.Clear();
                            pb.Minimum = 0;
                            pb.Maximum = fi.Length - 1;

                            string out_dir = AppDomain.CurrentDomain.BaseDirectory + "\\Unpacked";
                            if (!Directory.Exists(out_dir))
                                Directory.CreateDirectory(out_dir);

                            for (int i = 0; i < fi.Length; i++)
                            {
                                try
                                {
                                    string extr_files_log = AppDomain.CurrentDomain.BaseDirectory + "\\log.txt";

                                    if (File.Exists(extr_files_log)) File.Delete(extr_files_log);

                                    UPKpacker.UPKunpack(fi[i].FullName, out_dir, true, extr_files_log);

                                    string log = null;

                                    if (File.Exists(extr_files_log))
                                    {
                                        log = File.ReadAllText(extr_files_log);
                                        File.Delete(extr_files_log);
                                    }

                                    if (log != null) LogTxtBox.Document.Blocks.Add(new Paragraph(new Run(log)));
                                    else MessageBox.Show("Если не написался отчёт о распаковке, сообщи мне в ЛС (pashok6798).", "Странно", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                catch
                                {
                                    LogTxtBox.Document.Blocks.Add(new Paragraph(new Run("Что-то пошло не так. Или это файл не от Unreal Engine, или этот архив не поддерживается оболочкой.")));
                                }

                                pb.Value = i;
                            }
                        }
                        //else MessageBox.Show("Укажите правильный путь к папке с архивами");
                    }
                }
            }
        }
    }
}
