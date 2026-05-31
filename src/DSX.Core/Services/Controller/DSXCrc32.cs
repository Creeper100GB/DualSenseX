using System;

namespace DSX.Core.Services.Controller;

public static class DSXCrc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }
        return table;
    }

    public static uint Compute(byte[] data, int offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        int end = offset + length;
        for (int i = offset; i < end; i++)
            crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
        return ~crc;
    }

    public static void WriteCrc(byte[] report)
    {
        byte[] crcInput = new byte[75];
        crcInput[0] = 0xA2;
        crcInput[1] = report[0];
        Buffer.BlockCopy(report, 1, crcInput, 2, Math.Min(72, report.Length - 1));

        uint crc = Compute(crcInput, 0, 75);
        report[74] = (byte)(crc & 0xFF);
        report[75] = (byte)((crc >> 8) & 0xFF);
        report[76] = (byte)((crc >> 16) & 0xFF);
        report[77] = (byte)((crc >> 24) & 0xFF);
    }
}
