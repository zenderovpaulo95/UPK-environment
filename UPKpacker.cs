using System.Runtime.InteropServices;

namespace UPK_Environment
{
    internal class UPKpacker
    {
        [DllImport("UPKPacker.DLL", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern int UPKrepack(string SourceDir, string OutDir = null, bool OutPutLog = false, string LogFileName = null);

        [DllImport("UPKPacker.DLL", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern int UPKunpack(string FileName, string OutDir = null, bool OutPutLog = false, string LogFileName = null);

        [DllImport("UPKPacker.DLL", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern string PrintVer();

        [DllImport("lzo2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int __lzo_init_v2(uint v, int s1, int s2, int s3, int s4, int s5, int s6, int s7, int s8, int s9);

        [DllImport("lzo2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lzo1x_1_compress(byte[] src, int src_len, byte[] dest, ref int dest_len, byte[] WorkMem);
    }
}
