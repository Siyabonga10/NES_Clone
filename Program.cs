using System;

namespace _6502Clone
{
    using _6502Clone;
    class Program
    {
        static void Main(string[] args)
        {
           Bus.Bus myBus = new();
           CPU myCPU = new(ref myBus);
           byte[] data = File.ReadAllBytes("D:\\NES_Emualator\\6502Clone\\res\\test.bin");
           myBus.LoadData(0x8000, data);
           myCPU.Boot();
           myCPU.Run();
        }
    }
}