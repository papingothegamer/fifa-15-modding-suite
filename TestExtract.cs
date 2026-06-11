using System;
using System.IO;
using System.Reflection;

class Program {
    static void Main() {
        try {
            string rx3Path = @"M:\FUN\GAMES\FIFA 15\data\sceneassets\shoe\shoe_0_0_textures.rx3";
            byte[] fileBytes = File.ReadAllBytes(rx3Path);
            int ddsIndex = -1;
            for (int i = 0; i < fileBytes.Length - 4; i++) {
                if (fileBytes[i] == 0x44 && fileBytes[i+1] == 0x44 && fileBytes[i+2] == 0x53 && fileBytes[i+3] == 0x20) {
                    if (fileBytes[i+4] == 0x7C && fileBytes[i+5] == 0x00 && fileBytes[i+6] == 0x00 && fileBytes[i+7] == 0x00) {
                        ddsIndex = i;
                        break;
                    }
                }
            }
            Console.WriteLine("DDS Index: " + ddsIndex);
            if (ddsIndex != -1) {
                byte[] ddsBytes = new byte[fileBytes.Length - ddsIndex];
                Array.Copy(fileBytes, ddsIndex, ddsBytes, 0, ddsBytes.Length);
                File.WriteAllBytes("test_extracted.dds", ddsBytes);
                Console.WriteLine("Saved test_extracted.dds (" + ddsBytes.Length + " bytes)");
            }
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
