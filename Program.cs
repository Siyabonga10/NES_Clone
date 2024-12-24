using System;

namespace _6502Clone
{
    using _6502Clone;
    class Program
    {
        static void Main(string[] args)
        {
           Bus.Bus myBus = new();
           _6502Clone.CPU myCPU = new(ref myBus);
        }
    }
}