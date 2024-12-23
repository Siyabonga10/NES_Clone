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
        sbyte X, Y, A, P; // 8 bit registers: X and Y index registers, Stack ponter, Accumulator, Process status register
        byte S; // Stack pointer
        uint_16 PC; // program counter register
        ExternalBus bus_;

        public CPU(ref ExternalBus bus)
        {
            bus_ = bus;
            X = 0; Y = 0; S = 0; A = 0; P = 0;
            PC = 0;

            P ^= (sbyte)ProcessStatusFlags.I;
        }

        private void BootSequence()
        {
            PC = 0x000;
        }
        // Define cpu instructions 
        private void ADC(ref sbyte operand) {
            int result = A + (operand + (P >> (int)ProcessStatusFlags.C & 1));
            A = (sbyte)result;
            ToggleFlag(ProcessStatusFlags.C, result > 0XFF);
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ A) & (A ^ operand) & 0x80) == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void AND(ref sbyte operand) {
            A &= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N,  (A >> 7) == 1);
        }
        private void ASL(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand >> 7) == 1);
            operand <<= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void BBC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.C)) PC += (uint_16)operand;} // TODO: THE operand (offset) is in twos complement, so normal addition wont work for most of these branching instructions
        private void BCS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.C)) PC += (uint_16)operand;}
        private void BEQ(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.Z)) PC += (uint_16)operand;}
        private void BMI(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.N)) PC += (uint_16)operand;}
        private void BNE(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.Z)) PC += (uint_16)operand;}
        private void BPL(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.N)) PC += (uint_16)operand;}
        private void BVC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.V)) PC += (uint_16)(operand + 2);}
        private void BVS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.V)) PC += (uint_16)operand;}

        private void CLC(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.C);}
        private void CLD(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.D);}
        private void CLI(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.I);}
        private void CLV(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.V);}
        private void CMP(ref sbyte operand) {
            int res = A - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, A >= operand);
        }
        private void CPX(ref sbyte operand) {
            int res = X - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, X >= operand);
        }
        private void CPY(ref sbyte operand) {
            int res = Y - operand;
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, res < 0);
            ToggleFlag(ProcessStatusFlags.C, Y >= operand);
        }

        private void DEC(ref sbyte operand) {
            operand -= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void DEY(ref sbyte operand) {
            Y -= 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void DEX(ref sbyte operand) {
            X -= 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void EOR(ref sbyte operand) {
            operand ^= A;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void INC(ref sbyte operand) {
            operand += 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void INY(ref sbyte operand) {
            Y += 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void INX(ref sbyte operand) {
            X += 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void LDA(ref sbyte operand) {
            A = operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void LDX(ref sbyte operand) {
            X = operand;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X >> 7) == 1);
        }
        private void LDY(ref sbyte operand) {
            Y = operand;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y >> 7) == 1);
        }
        private void LSR(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand & (byte)0x01) == 1);
            operand >>= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand >> 7) == 1);
        }
        private void NOP(ref sbyte operand) {}
        private void ORA(ref sbyte operand) { 
            A |= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void PHA(ref sbyte operand) 
        {
            bus_.SetAddressValue(S);    // TODO: Change the communication between the cpu and the bus to simply future tasks
            bus_.SetDataValue(operand);
            S -= 1;
        }

        private void PHP(ref sbyte operand) 
        {
            ToggleFlag(ProcessStatusFlags.B, true);
            bus_.SetAddressValue(S);
            bus_.SetDataValue(P);
            S -= 1;
        }

        private void PLA(ref sbyte operand) 
        {
            S += 1;
            bus_.SetAddressValue(S);
            A = bus_.GetDataValue();
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }

        private void ROL(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (P & 0x80) == 1);
            operand = (sbyte)((operand << 1) | (operand >> 7));
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 1);
        }
        private void ROR(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (P & 0x80) == 1);
            operand = (sbyte)((operand >> 1) | (operand << 7));
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 1);

        }
        private void SEC(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.C);}
        private void SED(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.D);}
        private void SEI(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.I);}
        private void TAX(ref sbyte operand) {
            X = A;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 1);
        }
        private void TAY(ref sbyte operand) {
            Y = A;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 1);
        }
        private void TSX(ref sbyte operand) {
            X = (sbyte)S;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 1);
        }
        private void TXA(ref sbyte operand) {
            A = X;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 1);
        }
        private void TXS(ref sbyte operand) {
            S = (byte)X;
            ToggleFlag(ProcessStatusFlags.Z, S == 0);
            ToggleFlag(ProcessStatusFlags.N, (S & 0x80) == 1);
        }

        // TODO: Add implementation for the following instructions
        private void BIT(ref sbyte operand) {
            sbyte result = (sbyte)(A & operand);
            ToggleFlag(ProcessStatusFlags.Z, result == 0);
            ToggleFlag(ProcessStatusFlags.V, (result & 0x70) == 1);
            ToggleFlag(ProcessStatusFlags.N, (result & 0x80) == 1);
        }
        private void BRK(ref sbyte operand) {}
        private void JMP(ref uint_16 operand) {
            PC = operand;
        }
        private void JSR(ref uint_16 operand) {}
        private void PLP(ref sbyte operand) {
            S += 1;
            bus_.SetAddressValue(S);
            P = bus_.GetDataValue();

        }
        private void RTI(ref sbyte operand) {}
        private void RTS(ref sbyte operand) {}
        private void SBC(ref sbyte operand) {
            A = (sbyte)(A - operand - ~(int)ProcessStatusFlags.C & 1);
            ToggleFlag(ProcessStatusFlags.C, !(A < 0x00));
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ A) & (A ^ ~operand) & 0x80) == 0);
            ToggleFlag(ProcessStatusFlags.N, (A >> 7) == 1);
        }
        private void STA(ref sbyte operand) { operand = A; }
        private void STX(ref sbyte operand) { operand = X;}
        private void STY(ref sbyte operand) { operand = Y;}

        private bool IsFlagSet(ProcessStatusFlags flag)
        {
            return ((P >> (sbyte)flag) & 1) == 1;
        }

        private void ToggleFlagOn(ProcessStatusFlags flag)
        {
            sbyte mask = (sbyte)(1 << (sbyte)flag);
            P |=  mask;
        }

        private void ToggleFlagOff(ProcessStatusFlags flag)
        {
            sbyte mask = (sbyte)(1 << (sbyte)flag);
            P &= (sbyte) ~mask;
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