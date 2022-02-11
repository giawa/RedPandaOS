using System;
using System.IO;

namespace BuildTools
{
    public static class FontConverter
    {
        public static byte[] FromFnt(string filename)
        {
            using (StreamReader stream = new StreamReader(filename))
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter output = new BinaryWriter(ms))
            {
                for (int i = 0; i < 3; i++) stream.ReadLine();

                int count = int.Parse(stream.ReadLine().Substring(12));

                output.Write(count);
                output.Write(8);

                while (!stream.EndOfStream)
                {
                    string line = stream.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) break;
                    var split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    output.Write(byte.Parse(split[1].Substring(3)));
                    output.Write(byte.Parse(split[2].Substring(2)));
                    output.Write(byte.Parse(split[3].Substring(2)));
                    output.Write(byte.Parse(split[4].Substring(6)));
                    output.Write(byte.Parse(split[5].Substring(7)));
                    output.Write(sbyte.Parse(split[6].Substring(8)));
                    output.Write(sbyte.Parse(split[7].Substring(8)));
                    output.Write(byte.Parse(split[8].Substring(9)));
                }

                return ms.ToArray();
            }
        }
    }
}
