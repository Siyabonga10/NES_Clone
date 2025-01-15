namespace _6502Clone
{
    class Mappper000: Mapper
    {
        public override ushort? translateAddr(ushort cpuAddr)
        {
            return cpuAddr;
        }
    }
}