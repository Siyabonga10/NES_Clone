using System;

namespace _6502Clone
{
    using _6502;
    class Program
    {
        static void Main(string[] args)
        {
            Bus.Bus myBus = new();
            SysClock clock = new();
            CPU myCPU = new(ref myBus, ref clock);
            PPU myPPU = new(ref myBus, ref clock);
            Catridge test = new("D:\\nes\\6502Clone\\test_roms\\11-special.nes", ref myBus);
            myCPU.Boot();
            Thread CPU_thread = new(myCPU.RunTest);
            Thread PPU_thread = new(() =>
            {
                myPPU.InitDisplay();
                myPPU.RunPPU();
            });
            Thread Timer_thread = new(clock.RunClock);
            Timer_thread.Start();
            CPU_thread.Start();
            PPU_thread.Start();
            CPU_thread.Join();
            //clock.Finish();
            //myPPU.Finish();
            test.Finish();
        }
    }
}