namespace _6502Clone
{
    abstract class Mapper
    {
        public abstract ushort? translateAddr(ushort cpuAddr);
    }
}