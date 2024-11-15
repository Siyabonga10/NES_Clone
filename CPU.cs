using uint_8 = short;
using uint_16 = System.UInt16;
using ExternalBus = _6502Clone.Bus.Bus;

namespace _6502Clone
{
    enum ProcessStatusFlags
    {
        C, Z, I, D, B, UnusedFlag, V, N
    }
    class CPU
    {
        uint_8 X, Y, S, A, P; // 8 bit registers: X and Y index registers, Stack ponter, Accumulator, Process status register
        uint_16 PC; // program counter register
        ExternalBus bus_;

        public CPU(ref ExternalBus bus)
        {
            bus_ = bus;
            X = 0; Y = 0; S = 0; A = 0; P = 0;
            PC = 0;

            P ^= (uint_8)ProcessStatusFlags.I;
        }

        private void BootSequence()
        {
            PC = 0x000;
        }
    }
}