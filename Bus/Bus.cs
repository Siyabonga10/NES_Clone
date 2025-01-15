using System;

namespace _6502Clone.Bus
{
    delegate sbyte? BusReadable(ushort addr);
    delegate void BusWritable(ushort addr, sbyte value);
    class Bus
    {
        ushort addressValue;
        sbyte dataValue;
        readonly sbyte[] RAM = new sbyte[(int)Math.Pow(2, 16)];

        private readonly List<BusReadable> readCallbacks;    
        private readonly List<BusWritable> writeCallbacks;
        private sbyte tmpBuffer;

        public Bus()
        {
            readCallbacks = [];
            writeCallbacks = [];

            dataValue = 0;
            addressValue = 0;
            RAM[0xFFFC] = 0x00;
            RAM[0xFFFD] = -0x80;
        }

        public void LoadData(int startingAddress, byte[] data)
        {
            foreach(byte entry in data)
            {
                RAM[startingAddress] = (sbyte)entry;
                startingAddress += 1;
            }
        }   

        public void RegisterForReads(BusReadable callback) { readCallbacks.Add(callback);}

        public void RegisterForWrites(BusWritable callback){ writeCallbacks.Add(callback);}

        public ushort GetAddressValue() {return addressValue;}          // Returns a 16 byte value from memory based on the currently set address, little endian format
        public ref sbyte GetDataValue() {
            if(addressValue < 0x8000)
            {
                return ref RAM[addressValue];
            }

            foreach (BusReadable readCallback in readCallbacks)
            {
                if(readCallback(addressValue) is not null)
                {
                    tmpBuffer = (sbyte)readCallback(addressValue);
                    return ref tmpBuffer;
                }
            } 
            
            return ref tmpBuffer;
        }
        public void SetAddressValue(ushort value) {addressValue = value;}   
        public void SetDataValue(sbyte value) {
            if(addressValue < 0x8000)
            {
                RAM[addressValue] = value;
                return;
            }
            foreach(BusWritable writeCallback in writeCallbacks)
            {
                writeCallback(addressValue, value);
            }
        }
    }
}