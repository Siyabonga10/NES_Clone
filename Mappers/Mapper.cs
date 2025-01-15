namespace _6502Clone
{
    abstract class Mapper
    {
        public abstract ushort? TranslateAddr(ushort cpuAddr);
    }
}