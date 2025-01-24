namespace _6502Clone
{
    class Mappper000: Mapper
    {
        public override ushort? TranslateAddr(ushort cpuAddr)
        {
            if(0x6000 <= cpuAddr && cpuAddr < 0x8000)
            {
                return (ushort)(cpuAddr - 0x6000);
            }
            if(0x8000 < cpuAddr && cpuAddr < 0xFFFF)
            {
                return (ushort?)((ushort)16 + cpuAddr - 0x8000 + 0x2000);
            }
            return null;
        }
    }
}