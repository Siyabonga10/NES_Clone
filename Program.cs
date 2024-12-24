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
           byte[] data = new byte[1000];
           StreamReader sr = new("D:\\NES_Emualator\\6502Clone\\res\\test.bin");
           string inputData = sr.ReadToEnd();
           data = System.Text.Encoding.UTF8.GetBytes(inputData);
           sr.Close();
           myBus.LoadData(0, data);
           myCPU.Run();
        }
    }
}