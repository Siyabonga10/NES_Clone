using System;

namespace _6502Clone.Bus
{
    class Bus
    {
        UInt16 addressValue;
        byte dataValue;

        public Bus()
        {
            dataValue = 0;
            addressValue = 0;
        }

        public UInt16 GetAddressValue() {return addressValue;}
        public UInt16 GetDataValue() {return dataValue;}
        public void SetAddressValue(UInt16 value) {addressValue = value;}   
        public void SetDataValue(byte value) {dataValue = value;}
    }
}