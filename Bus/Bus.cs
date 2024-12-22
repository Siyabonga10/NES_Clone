using System;

namespace _6502Clone.Bus
{
    class Bus
    {
        ushort addressValue;
        byte dataValue;

        public Bus()
        {
            dataValue = 0;
            addressValue = 0;
        }

        public ushort GetAddressValue() {return addressValue;}
        public byte GetDataValue() {return dataValue;}
        public void SetAddressValue(ushort value) {addressValue = value;}   
        public void SetDataValue(byte value) {dataValue = value;}
    }
}