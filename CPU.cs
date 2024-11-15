using uint_8 = byte;
using uint_16 = System.UInt16;
using ExternalBus = _6502Clone.Bus.Bus;
using System.Runtime.CompilerServices;
using System.Diagnostics;

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
        // Define cpu instructions 
        private void ADC(ref uint_8 operand) { A += (uint_8)(operand + (P >> (int)ProcessStatusFlags.C & 1));}
        private void AND(ref uint_8 operand) { A &= operand;}
        private void ASL(ref uint_8 operand) { operand <<= 1;}
        private void BBC(ref uint_8 operand) {if(!IsFlagSet(ProcessStatusFlags.C)) PC += (uint_16)operand;}
        private void BCS(ref uint_8 operand) {if(IsFlagSet(ProcessStatusFlags.C)) PC += (uint_16)operand;}
        private void BEQ(ref uint_8 operand) {if(IsFlagSet(ProcessStatusFlags.Z)) PC += (uint_16)operand;}
        private void BMI(ref uint_8 operand) {if(IsFlagSet(ProcessStatusFlags.N)) PC += (uint_16)operand;}
        private void BNE(ref uint_8 operand) {if(!IsFlagSet(ProcessStatusFlags.Z)) PC += (uint_16)operand;}
        private void BPL(ref uint_8 operand) {if(!IsFlagSet(ProcessStatusFlags.N)) PC += (uint_16)operand;}
        private void BVC(ref uint_8 operand) {if(!IsFlagSet(ProcessStatusFlags.V)) PC += (uint_16)operand;}
        private void BVS(ref uint_8 operand) {if(IsFlagSet(ProcessStatusFlags.V)) PC += (uint_16)operand;}
        private void CLC(ref uint_8 operand) {ToggleFlagOff(ProcessStatusFlags.C);}
        private void CLD(ref uint_8 operand) {ToggleFlagOff(ProcessStatusFlags.D);}
        private void CLI(ref uint_8 operand) {ToggleFlagOff(ProcessStatusFlags.I);}
        private void CLV(ref uint_8 operand) {ToggleFlagOff(ProcessStatusFlags.V);}
        private void CMP(ref uint_8 operand) {
            int res = A - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, A > operand);
        }
        private void CPX(ref uint_8 operand) {
            int res = X - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, X > operand);
        }
        private void CPY(ref uint_8 operand) {
            int res = Y - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, Y > operand);
        }

        private void DEC(ref uint_8 operand) {operand -= 1;}
        private void DEY(ref uint_8 operand) {Y -= 1;}
        private void DEX(ref uint_8 operand) {X -= 1;}
        private void EOR(ref uint_8 operand) {operand ^= A;}
        private void INC(ref uint_8 operand) {operand += 1;}
        private void INY(ref uint_8 operand) {Y += 1;}
        private void INX(ref uint_8 operand) {X += 1;}
        private void LDA(ref uint_8 operand) {A = operand;}
        private void LDX(ref uint_8 operand) {X = operand;}
        private void LDY(ref uint_8 operand) {Y = operand;}
        private void LSR(ref uint_8 operand) { operand >>= 1;}
        private void NOP(ref uint_8 operand) {}
        private void ORA(ref uint_8 operand) { operand |= A;}
        private void PHA(ref uint_8 operand) 
        {
            bus_.SetAddressValue(S);
            bus_.SetDataValue(operand);
            S += 1;
        }

        private void PHP(ref uint_8 operand) 
        {
            bus_.SetAddressValue(S);
            bus_.SetDataValue(operand);
            S += 1;
        }

        private void ROL(ref uint_8 operand) {operand = (uint_8)((operand << 1) | (operand >> 7));}

        private bool IsFlagSet(ProcessStatusFlags flag)
        {
            return ((P >> (uint_8)flag) & 1) == 1;
        }

        private void ToggleFlagOn(ProcessStatusFlags flag)
        {
            uint_8 mask = (uint_8)(1 << (uint_8)flag);
            P |=  mask;
        }

        private void ToggleFlagOff(ProcessStatusFlags flag)
        {
            uint_8 mask = (uint_8)(1 << (uint_8)flag);
            P &= (uint_8) ~mask;
        }                

        private void ToggleFlag(ProcessStatusFlags flag, bool state)
        {
            if(state)
                ToggleFlagOn(flag);
            else
                ToggleFlagOff(flag);
        }

    }
}