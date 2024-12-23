using System;

namespace _6502Clone.Bus
{
    class Bus
    {
        ushort addressValue;
        sbyte dataValue;

        public Bus()
        {
            dataValue = 0;
            addressValue = 0;
        }

        public ushort GetAddressValue() {return addressValue;}          // Returns a 16 byte value from memory based on the currently set address, little endian format
        public ref sbyte GetDataValue() {return ref dataValue;}
        public void SetAddressValue(ushort value) {addressValue = value;}   
        public void SetDataValue(sbyte value) {dataValue = value;}
    }
}