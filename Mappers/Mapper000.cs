namespace _6502Clone
{
    class Mappper000: Mapper
    {
        public override ushort? TranslateAddr(ushort cpuAddr)
        {
            return cpuAddr;
        }
    }
}