using System;

namespace _6502Clone.Bus
{
    class Bus
    {
        ushort addressValue;
        sbyte dataValue;
        readonly sbyte[] RAM = new sbyte[2^16];

        public Bus()
        {
            dataValue = 0;
            addressValue = 0;
        }

        public void LoadData(int startingAddress, sbyte[] data)
        {
            foreach(sbyte entry in data)
            {
                RAM[startingAddress] = entry;
                startingAddress += 1;
            }
        }

        public ushort GetAddressValue() {return addressValue;}          // Returns a 16 byte value from memory based on the currently set address, little endian format
        public ref sbyte GetDataValue() {return ref RAM[dataValue];}
        public void SetAddressValue(ushort value) {addressValue = value;}   
        public void SetDataValue(sbyte value) {dataValue = value;}
    }
}