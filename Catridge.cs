using _6502Clone.Bus;

namespace _6502Clone
{
    class Catridge
    {
        private readonly byte[] mem;
        private readonly Mapper mapper;
        private readonly int catridgeSize;
        public Catridge(string rom_file_path, ref Bus.Bus bus)
        {
            mapper = new Mappper000();
            mem = File.ReadAllBytes(rom_file_path);
            catridgeSize = mem.Length;
            bus.RegisterForReads(Read);
            bus.RegisterForWrites(Write);
        }

        public byte? Read(ushort addr)
        {
            ushort? translatedAddr = mapper.TranslateAddr(addr);
            return translatedAddr is null ? null: (byte) mem[(int)translatedAddr];
        }

        public void Write(ushort addr, sbyte value)
        {
            ushort? translatedAddr = mapper.TranslateAddr(addr);
            if(translatedAddr is null) return;
            mem[(int)translatedAddr] = (byte)value;
        }
    }
}