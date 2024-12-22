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
        private void ADC(ref uint_8 operand) {
            int result = A + (operand + (P >> (int)ProcessStatusFlags.C & 1));
            A = (uint_8)result;
            ToggleFlag(ProcessStatusFlags.C, result > 0XFF);
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ A) & (A ^ operand) & 0x80) == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void AND(ref uint_8 operand) {
            A &= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N,  (A >> 7) == 1);
        }
        private void ASL(ref uint_8 operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand >> 7) == 1);
            operand <<= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void BBC(ref uint_8 operand) {if(!IsFlagSet(ProcessStatusFlags.C)) PC += (uint_16)operand;} // TODO: THE operand (offset) is in twos complement, so normal addition wont work for most of these branching instructions
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
            ToggleFlag(ProcessStatusFlags.C, A >= operand);
        }
        private void CPX(ref uint_8 operand) {
            int res = X - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, X >= operand);
        }
        private void CPY(ref uint_8 operand) {
            int res = Y - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, Y >= operand);
        }

        private void DEC(ref uint_8 operand) {
            operand -= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void DEY(ref uint_8 operand) {
            Y -= 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void DEX(ref uint_8 operand) {
            X -= 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void EOR(ref uint_8 operand) {
            operand ^= A;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void INC(ref uint_8 operand) {
            operand += 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void INY(ref uint_8 operand) {
            Y += 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void INX(ref uint_8 operand) {
            X += 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void LDA(ref uint_8 operand) {
            A = operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void LDX(ref uint_8 operand) {
            X = operand;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void LDY(ref uint_8 operand) {
            Y = operand;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void LSR(ref uint_8 operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand & (byte)0x01) == 1);
            operand >>= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void NOP(ref uint_8 operand) {}
        private void ORA(ref uint_8 operand) { 
            A |= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void PHA(ref uint_8 operand) 
        {
            bus_.SetAddressValue(S);    // TODO: Change the communication between the cpu and the bus to simply future tasks
            bus_.SetDataValue(operand);
            S -= 1;
        }

        private void PHP(ref uint_8 operand) 
        {
            ToggleFlag(ProcessStatusFlags.B, true);
            bus_.SetAddressValue(S);
            bus_.SetDataValue(P);
            S -= 1;
        }

        private void PLA(ref uint_8 operand) 
        {
            S += 1;
            bus_.SetAddressValue(S);
            A = bus_.GetDataValue();
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }

        private void ROL(ref uint_8 operand) {
            ToggleFlag(ProcessStatusFlags.C, (P & 0x80) == 1);
            operand = (uint_8)((operand << 1) | (operand >> 7));
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 1);
        }
        private void ROR(ref uint_8 operand) {
            ToggleFlag(ProcessStatusFlags.C, (P & 0x80) == 1);
            operand = (uint_8)((operand >> 1) | (operand << 7));
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 1);

        }
        private void SEC(ref uint_8 operand) {ToggleFlagOn(ProcessStatusFlags.C);}
        private void SED(ref uint_8 operand) {ToggleFlagOn(ProcessStatusFlags.D);}
        private void SEI(ref uint_8 operand) {ToggleFlagOn(ProcessStatusFlags.I);}
        private void TAX(ref uint_8 operand) {
            X = A;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 1);
        }
        private void TAY(ref uint_8 operand) {
            Y = A;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 1);
        }
        private void TSX(ref uint_8 operand) {
            X = S;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 1);
        }
        private void TXA(ref uint_8 operand) {
            A = X;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 1);
        }
        private void TXS(ref uint_8 operand) {
            S = X;
            ToggleFlag(ProcessStatusFlags.Z, S == 0);
            ToggleFlag(ProcessStatusFlags.N, (S & 0x80) == 1);
        }

        // TODO: Add implementation for the following instructions
        private void BIT(ref uint_8 operand) {}
        private void BRK(ref uint_8 operand) {}
        private void JMP(ref uint_16 operand) {}
        private void JSR(ref uint_16 operand) {}
        private void PLP(ref uint_8 operand) {}
        private void RTI(ref uint_8 operand) {}
        private void RTS(ref uint_8 operand) {}
        private void SBC(ref uint_8 operand) {}
        private void STA(ref uint_8 operand) {}
        private void STX(ref uint_8 operand) {}
        private void STY(ref uint_8 operand) {}

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