using _6502Clone.Bus;

namespace _6502
{
    class PPU
    {
        private readonly byte[] registers;
        public PPU(ref Bus bus)
        {
            registers = [0, 0, 0, 0, 0, 0, 0, 0];
            bus.RegisterForReads(Read);
        }

        public byte? Read(ushort addr)
        {
            if(0x2000 <= addr && addr < 0x3FFF)
            {
                return registers[(addr - 0x2000) % 8];
            }
            return null;
        }
    }

}