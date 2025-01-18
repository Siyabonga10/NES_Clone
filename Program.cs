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
           Catridge test = new("D:\\nes\\6502Clone\\test_roms\\01-implied.nes", ref myBus);
           myCPU.Boot();
           myCPU.Run();
        }
    }
}