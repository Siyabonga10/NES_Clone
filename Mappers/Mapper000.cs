namespace _6502Clone
{
    class Mapper000: Mapper
    {
        private uint CHR_OFFSET;
        public Mapper000(byte[] header) : base(header)
        {
            CHR_OFFSET = 16;
            if ((GetHeaderByte(6) & 2) != 0) CHR_OFFSET += 512;
            CHR_OFFSET += (uint)(0x4000 * GetHeaderByte(4));
            Console.WriteLine(CHR_OFFSET.ToString());
        }
        public override ushort? TranslateAddr(ushort cpuAddr)
        {
            if(0x6000 <= cpuAddr && cpuAddr < 0x8000)
            {
                return (ushort)(cpuAddr - 0x6000);
            }
            if(0x8000 < cpuAddr && cpuAddr <= 0xFFFF)
            {
                return (ushort?)((ushort)16 + cpuAddr - 0x8000 + 0x2000);
            }
            return null;
        }

        public override ushort? TranslatePPUAddr(ushort ppuAddr)
        {
            // PPU may try to access the pattern tables, CHR_RAM/ROM or the nametables, currently not sure where the palletes would be stored ...

            // pattern table access
            if(ppuAddr <= 0x1FFF)
            {
                return (ushort)(CHR_OFFSET + 0x2000 + ppuAddr);
            }
            return null;
        }
    }
}