using _6502Clone.Bus;

namespace _6502Clone
{
    class Catridge
    {
        private readonly byte[] mem;
        private readonly Mapper mapper;
        private readonly string filename;
        public Catridge(string rom_file_path, ref Bus.Bus bus)
        {
            
            byte[] memBytes = File.ReadAllBytes(rom_file_path);
            mem = new byte[memBytes.Length + 0x2000]; // Catridge rom + ram
            Array.Copy(memBytes, 0, mem, 0x2000, memBytes.Length);
            bus.RegisterForReads(Read);
            bus.RegisterForWrites(Write);
            filename = rom_file_path.Split('/').Last();
            bus.RegisterForPPUReads(PPURead);

            byte[] header = new byte[16];
            Array.Copy(memBytes, header, header.Length);
            mapper = new Mapper000(header);

        }

        public byte? Read(ushort addr)
        {
            ushort? translatedAddr = mapper.TranslateAddr(addr);
            return translatedAddr is null ? null: mem[(int)translatedAddr];
        }

        public byte? PPURead(ushort addr)
        {
            ushort? translatedAddr = mapper.TranslatePPUAddr(addr);
            return translatedAddr is null ? null : mem[(int)translatedAddr];
        }

        public void Write(ushort addr, sbyte value)
        {
            ushort? translatedAddr = mapper.TranslateAddr(addr);
            if(translatedAddr is null) return;
            mem[(int)translatedAddr] = (byte)value;
        }

        public void Finish()
        {
            Console.WriteLine("Saving test results..." + filename);
            byte[] test_result = new byte[0x8000 - 0x6004];
            Array.Copy(mem, 4, test_result, 0, 0x8000 - 0x6004);
            string tmp = System.Text.Encoding.ASCII.GetString(test_result);
            Console.WriteLine(tmp);
            File.WriteAllText(filename + ".test_result", tmp);
        }
    }
}