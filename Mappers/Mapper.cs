namespace _6502Clone
{
    public abstract class Mapper
    {
        private byte[] header;
        public Mapper(byte[] headerData) { 
            header = headerData;
            if (header.Length != 16) throw new ArgumentException("Unkown ROM format.");
        }
        public abstract ushort? TranslateAddr(ushort cpuAddr);
        public abstract ushort? TranslatePPUAddr(ushort ppuAddr); 

        public byte GetHeaderByte(int index)
        {
            if(index < 0 || index >= header.Length) throw new ArgumentOutOfRangeException("index");
            return header[index];
        }
    }
}