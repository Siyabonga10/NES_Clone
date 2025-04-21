using uint_16 = System.UInt16;
using ExternalBus = _6502Clone.Bus.Bus;
using System.Diagnostics;
using System;

delegate void Instruction(ref sbyte operand);
delegate ref sbyte AddressingMode();

readonly struct ExecutionInfo(Instruction inst, AddressingMode addrMode, byte clockCycles)
{
    readonly public Instruction instruction = inst;
    readonly public AddressingMode addressingMode = addrMode;
    readonly public byte Cycles = clockCycles;
}

namespace _6502Clone
{
    enum ProcessStatusFlags
    {
        C, Z, I, D, B, UnusedFlag, V, N
    }
    class CPU
    {     
        readonly private List<List<ExecutionInfo>> OpCodeMatrix;
        sbyte X, Y, A, P; // 8 bit registers: X and Y index registers, Stack ponter, Accumulator, Process status register
        byte S; // Stack pointer
        uint_16 PC; // program counter register
        ushort addrBuffer;
        ExternalBus bus_;

        // Debugging stuff
        bool testStarted;
        ushort breakPt;
        bool targetReached;

        bool usesAcc;

        // Timing stuff
        private int clocksTicks;
        private const int CLOCK_TICKS_MAX = 4;
        private bool ready;
        private int required_clock_cycles;

        // CPU power switch
        private bool CPU_on;

        public CPU(ref ExternalBus bus, ref SysClock clock)
        {
            CPU_on = true;
            required_clock_cycles = 0;
            clock.RegisterForTicks(Tick);
            ready = false;
            clocksTicks = 0;
            testStarted = false;
            breakPt = 0xE193;
            targetReached = false;

            bus_ = bus;
            X = 0; Y = 0; S = 0; A = 0; P = 0;
            PC = 0;
            usesAcc = false;
            P ^= (sbyte)ProcessStatusFlags.I;
            OpCodeMatrix = [
                [new(BRK, Stack, 7), new(ORA, ZPIndxInd, 6), new(NOP, IMP, 0), new(NOP, ZPIndxInd, 8), new(NOP, ZP, 3), new(ORA, ZP, 3), new(ASL, ZP, 5), new(NOP, ZP, 5), new(PHP, Stack, 3), new(ORA, IMM, 2), new(ASL, ACC, 2), new(NOP, IMM, 2), new(NOP, Abs, 4), new(ORA, Abs, 4), new(ASL, Abs, 6), new(NOP, Abs, 6)],
                [new(BPL, PCR, 2), new(ORA, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(ORA, ZPIndxX, 4), new(ASL, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(CLC, IMP, 2), new(ORA, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 7), new(NOP, AbsIndxX, 4), new(ORA, AbsIndxX, 4), new(ASL, AbsIndxX, 7), new(NOP, AbsIndxX, 7)],
                [new(JSR, IMP, 6), new(AND, ZPIndxInd, 6), new(NOP, IMP, 0), new(NOP, ZPIndxInd, 8), new(BIT, ZP, 3), new(AND, ZP, 3), new(ROL, ZP, 5), new(NOP, ZP, 5), new(PLP, Stack, 4), new(AND, IMM, 2), new(ROL, ACC, 2), new(NOP, IMM, 2), new(BIT, Abs, 4), new(AND, Abs, 4), new(ROL, Abs, 6), new(NOP, Abs, 6)],
                [new(BMI, PCR, 2), new(AND, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(AND, ZPIndxX, 4), new(ROL, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(SEC, IMP, 2), new(AND, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 7), new(NOP, AbsIndxX, 4), new(AND, AbsIndxX, 4), new(ROL, AbsIndxX, 7), new(NOP, AbsIndxX, 7)],

                [new(RTI, Stack, 6), new(EOR, ZPIndxInd, 6), new(NOP, IMP, 0), new(NOP, ZPIndxInd, 8), new(NOP, ZP, 3), new(EOR, ZP, 3), new(LSR, ZP, 3), new(NOP, ZP, 5), new(PHA, Stack, 3), new(EOR, IMM, 2), new(LSR, ACC, 2), new(NOP, IMM, 2), new(JMP, Abs, 3), new(EOR, Abs, 4), new(LSR, Abs, 6), new(NOP, Abs, 6)],
                [new(BVC, PCR, 2), new(EOR, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(EOR, ZPIndxX, 4), new(LSR, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(CLI, IMP, 2), new(EOR, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 7), new(NOP, AbsIndxX, 4), new(EOR, AbsIndxX, 4), new(LSR, AbsIndxX, 7), new(NOP, AbsIndxX, 0)],
                [new(RTS, IMP, 6), new(ADC, ZPIndxInd, 6), new(NOP, IMP, 0), new(NOP, ZPIndxInd, 8), new(NOP, ZP, 3), new(ADC, ZP, 3), new(ROR, ZP, 3), new(NOP, ZP, 5), new(PLA, Stack, 4), new(ADC, IMM, 2), new(ROR, ACC, 2), new(NOP, IMM, 2), new(JMP, AbsInd, 5), new(ADC, Abs, 4), new(ROR, Abs, 6), new(NOP, Abs, 6)],
                [new(BVS, PCR, 2), new(ADC, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(ADC, ZPIndxX, 4), new(ROR, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(SEI, IMP, 2), new(ADC, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 7), new(NOP, AbsIndxX, 4), new(ADC, AbsIndxX, 4), new(ROR, AbsIndxX, 7), new(NOP, AbsIndxX, 7)],
                
                [new(NOP, IMM, 2), new(STA, ZPIndxInd, 6), new(NOP, IMM, 2), new(NOP, ZPIndxInd, 6), new(STY, ZP, 3), new(STA, ZP, 3), new(STX, ZP, 0), new(NOP, ZP, 3), new(DEY, IMP, 2), new(NOP, IMM, 2), new(TXA, IMP, 2), new(NOP, IMM, 2), new(STY, Abs, 4), new(STA, Abs, 4), new(STX, Abs, 4), new(NOP, Abs, 4)],
                [new(BCC, PCR, 2), new(STA, ZPIndIndxY, 6), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 6), new(STY, ZPIndxX, 4), new(STA, ZPIndxX, 4), new(STX, ZPIndxY, 4), new(NOP, ZPIndxY, 4), new(TYA, IMP, 2), new(STA, AbsIndxY, 5), new(TXS, IMP, 2), new(NOP, AbsIndxY, 5), new(NOP, AbsIndxX, 5), new(STA, AbsIndxX, 5), new(NOP, AbsIndxY, 5), new(NOP, AbsIndxY, 5)],
                [new(LDY, IMM, 2), new(LDA, ZPIndxInd, 6), new(LDX, IMM, 0), new(NOP, ZPIndxInd, 6), new(LDY, ZP, 3), new(LDA, ZP, 3), new(LDX, ZP, 3), new(NOP, ZP, 3), new(TAY, IMP, 2), new(LDA, IMM, 2), new(TAX, IMP, 2), new(NOP, IMM, 2), new(LDY, Abs, 4), new(LDA, Abs, 4), new(LDX, Abs, 4), new(NOP, Abs, 4)],
                [new(BCS, PCR, 2), new(LDA, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 5), new(LDY, ZPIndxX, 4), new(LDA, ZPIndxX, 4), new(LDX, ZPIndxY, 4), new(NOP, ZPIndxY, 4), new(CLV, IMP, 2), new(LDA, AbsIndxY, 4), new(TSX, IMP, 2), new(NOP, AbsIndxY, 4), new(LDY, AbsIndxX, 4), new(LDA, AbsIndxX, 4), new(LDX, AbsIndxY, 4), new(NOP, AbsIndxY, 4)],
     
                [new(CPY, IMM, 2), new(CMP, ZPIndxInd, 6), new(NOP, IMM, 2), new(NOP, ZPIndxInd, 8), new(CPY, ZP, 3), new(CMP, ZP, 3), new(DEC, ZP, 5), new(NOP, ZP, 5), new(INY, IMP, 2), new(CMP, IMM, 2), new(DEX, IMP, 2), new(NOP, IMM, 2), new(CPY, Abs, 4), new(CMP, Abs, 4), new(DEC, Abs, 6), new(NOP, Abs, 6)],
                [new(BNE, PCR, 2), new(CMP, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(CMP, ZPIndxX, 4), new(DEC, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(CLD, IMP, 2), new(CMP, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 7), new(NOP, AbsIndxX, 4), new(CMP, AbsIndxX, 4), new(DEC, AbsIndxX, 7), new(NOP, AbsIndxX, 7)],
                [new(CPX, IMM, 2), new(SBC, ZPIndxInd, 6), new(NOP, IMM, 2), new(NOP, ZPIndxInd, 8), new(CPX, ZP, 3), new(SBC, ZP, 3), new(INC, ZP, 5), new(NOP, ZP, 5), new(INX, IMP, 2), new(SBC, IMM, 2), new(NOP, IMP, 2), new(NOP, IMM, 2), new(CPX, Abs, 4), new(SBC, Abs, 4), new(INC, Abs, 6), new(NOP, Abs, 6)],
                [new(BEQ, PCR, 2), new(SBC, ZPIndIndxY, 5), new(NOP, IMP, 0), new(NOP, ZPIndIndxY, 8), new(NOP, ZPIndxX, 4), new(SBC, ZPIndxX, 4), new(INC, ZPIndxX, 6), new(NOP, ZPIndxX, 6), new(SED, IMP, 2), new(SBC, AbsIndxY, 4), new(NOP, IMP, 2), new(NOP, AbsIndxY, 4), new(NOP, AbsIndxX, 2), new(SBC, AbsIndxX, 4), new(INC, AbsIndxX, 7), new(NOP, AbsIndxX, 7)],

     ];
        }

        public void Boot()
        {
            S = 0xFF;
            ushort tmpAddr;
            tmpAddr = 0xFFFC;
            bus_.SetAddressValue(tmpAddr);
            ushort lowerByte = (ushort)(byte)bus_.GetDataValue();
            tmpAddr += 1;
            bus_.SetAddressValue(tmpAddr);
            ushort upperByte = (ushort)(bus_.GetDataValue() << 8);
            PC = (ushort)(upperByte + lowerByte);  
        }
        // Define cpu instructions 
        private void ADC(ref sbyte operand) {
            sbyte tmpVar = A;
            ushort result = (ushort)((byte)A + (byte)operand + (IsFlagSet(ProcessStatusFlags.C) ? (byte)0x01 : (byte)0));
            A = (sbyte)(byte)result;
            ToggleFlag(ProcessStatusFlags.C,( result & 0x100) == 0x100);
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ tmpVar) & (A ^ operand) & 0x80) == 0x80);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void AND(ref sbyte operand) {
            A &= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N,  (A & 0x80) == 0x80);
        }
        private void ASL(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand & 0x80) == 0x80);
            sbyte res = (sbyte)(operand << 1);
            operand = res;
            if (!usesAcc) bus_.Write(addrBuffer, res);

            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 0x80);
        }
        private void BCC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.C)) PC = addrBuffer;}
        private void BCS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.C)) PC = addrBuffer;}
        private void BEQ(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.Z)) PC = addrBuffer;}
        private void BMI(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.N)) PC = addrBuffer;}
        private void BNE(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.Z)) PC = addrBuffer;}
        private void BPL(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.N)) PC = addrBuffer;}
        private void BVC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.V)) PC = addrBuffer;}
        private void BVS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.V)) PC = addrBuffer;}

        private void CLC(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.C);}
        private void CLD(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.D);}
        private void CLI(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.I);}
        private void CLV(ref sbyte operand) {ToggleFlagOff(ProcessStatusFlags.V);}
        private void CMP(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.Z, A == operand);
            ToggleFlag(ProcessStatusFlags.N, (((byte)A - (byte)operand) & 0x80) == 0x80);
            ToggleFlag(ProcessStatusFlags.C, (byte)A >= (byte)operand);
        }
        private void CPX(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.Z, X == operand);
            ToggleFlag(ProcessStatusFlags.N, ((byte)((byte)X - (byte)operand) & 0x80) == 0x80);
            ToggleFlag(ProcessStatusFlags.C, (byte)X >= (byte)operand);
        }
        private void CPY(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.Z, Y == operand);
            ToggleFlag(ProcessStatusFlags.N, ((byte)((byte)Y - (byte)operand) & 0x80) == 0x80);
            ToggleFlag(ProcessStatusFlags.C, (byte)Y >= (byte)operand);
        }

        private void DEC(ref sbyte operand) {
            sbyte res = (sbyte)(operand - 1);
            operand -= 1;
            if (!usesAcc) bus_.Write(addrBuffer, res);
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 0x80);
        }
        private void DEY(ref sbyte operand) {
            Y -= 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 0x80);
        }
        private void DEX(ref sbyte operand) {
            X -= 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 0x80);
        } 
        private void EOR(ref sbyte operand) {
            A ^= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void INC(ref sbyte operand) {
            sbyte res = (sbyte)(operand + 1);
            operand += 1;
            if (!usesAcc) bus_.Write(addrBuffer, res);
            ToggleFlag(ProcessStatusFlags.Z, res == 0);
            ToggleFlag(ProcessStatusFlags.N, (res & 0x80) == 0x80);
        }
        private void INY(ref sbyte operand) {
            Y += 1;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 0x80);
        }
        private void INX(ref sbyte operand) {
            X += 1;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 0x80);
        }
        private void LDA(ref sbyte operand) {
            A = operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void LDX(ref sbyte operand) {
            X = operand;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 0x80);
        }
        private void LDY(ref sbyte operand) {
            Y = operand;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 0x80);
        }
        private void LSR(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand & 0x01) == 1);
            sbyte result = (sbyte)((byte)operand >> 1);
            operand = result;
            if (!usesAcc)  bus_.Write(addrBuffer, result);
            ToggleFlag(ProcessStatusFlags.Z, result == 0);
            ToggleFlag(ProcessStatusFlags.N, (result & 0x80) == 0x80);
        }
        private void NOP(ref sbyte operand) {}
        private void ORA(ref sbyte operand) { 
            A |= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void PHA(ref sbyte operand) 
        {
            bus_.SetAddressValue((ushort)(0x100 + S));   
            bus_.SetDataValue(A);
            S -= 1;
        }

        private void PHP(ref sbyte operand) 
        {

            bus_.SetAddressValue((ushort)(0x100 + S));
            bus_.SetDataValue((sbyte)(P | 0x30));
            S -= 1;
        }

        private void PLA(ref sbyte operand) 
        {
            S += 1;
            bus_.SetAddressValue((ushort)(0x100 + S));
            A = bus_.GetDataValue();
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }

        private void ROL(ref sbyte operand) {
            byte carryValue = IsFlagSet(ProcessStatusFlags.C) ? (byte)0x01 : (byte)0x0;
            ToggleFlag(ProcessStatusFlags.C, (operand & 0x80) == 0x80);
            sbyte result = (sbyte)(carryValue + ((byte)operand << 1));
            operand = result;
            if (!usesAcc)  bus_.Write(addrBuffer, result);
            ToggleFlag(ProcessStatusFlags.Z, result == 0);
            ToggleFlag(ProcessStatusFlags.N, (result & 0x80) == 0x80);
        }
        private void ROR(ref sbyte operand) {
            byte carryValue = IsFlagSet(ProcessStatusFlags.C) ? (byte)0x80 : (byte)0;
            ToggleFlag(ProcessStatusFlags.C, (operand & 0x01) == 0x01);
            sbyte result = (sbyte)(carryValue + ((byte)operand >> 1));
            operand = result;
            if(!usesAcc) bus_.Write(addrBuffer, result);
            ToggleFlag(ProcessStatusFlags.Z, result == 0);
            ToggleFlag(ProcessStatusFlags.N, (result & 0x80) == 0x80);

        }
        private void SEC(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.C);}
        private void SED(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.D);}
        private void SEI(ref sbyte operand) {ToggleFlagOn(ProcessStatusFlags.I);}
        private void TAX(ref sbyte operand) {
            X = A;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 0x80);
        }
        private void TAY(ref sbyte operand) {
            Y = A;
            ToggleFlag(ProcessStatusFlags.Z, Y == 0);
            ToggleFlag(ProcessStatusFlags.N, (Y & 0x80) == 0x80);
        }
        private void TSX(ref sbyte operand) {
            X = (sbyte)S;
            ToggleFlag(ProcessStatusFlags.Z, X == 0);
            ToggleFlag(ProcessStatusFlags.N, (X & 0x80) == 0x80);
        }
        private void TXA(ref sbyte operand) {
            A = X;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void TYA(ref sbyte operand) {
            A = Y;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void TXS(ref sbyte operand) {
            S = (byte)X;
        }

        private void BIT(ref sbyte operand) {
            sbyte result = (sbyte)(A & operand);
            ToggleFlag(ProcessStatusFlags.Z, result == 0);
            ToggleFlag(ProcessStatusFlags.V, (operand & 0x40) == 0x40);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 0x80);
        }
        private void BRK(ref sbyte operand) {
            // push the PC onto the stack
            PC += 1;
            byte pcLow = (byte)(PC & 0x00FF);
            byte pcHigh = (byte)(PC >> 8);

            bus_.Write((ushort)(0x100 + S), (sbyte)pcHigh);
            S -= 1;
            bus_.Write((ushort)(0x100 + S), (sbyte)pcLow);
            S -= 1;
            
            // Push the status register onto the staack, with bits 4 and 5 set
            byte P_tmp = (byte)(P | 0x30);
            bus_.Write((ushort)(0x100 + S), (sbyte)P_tmp);
            S -= 1;

            // Jump to interrupt handler
            pcLow = (byte)bus_.Read(0xFFFE);
            pcHigh = (byte)bus_.Read(0xFFFF);
            ushort upper = (ushort)(pcHigh << 8);
            PC = (ushort)(pcLow + upper);
        }
        private void JMP(ref sbyte operand) {
            PC = addrBuffer;
        }
        private void JSR(ref sbyte operand) {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)bus_.GetDataValue();
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)(byte)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh);
            bus_.SetAddressValue(operandAddr);
            // Store the PC onto the stack
            sbyte pcLow = (sbyte)(PC & 0xFF);
            sbyte pcHigh = (sbyte)(PC >> 8);

            bus_.SetAddressValue((ushort)(0x100 + S));
            bus_.SetDataValue(pcHigh);
            S--;
            bus_.SetAddressValue((ushort)(0x100 + S));
            bus_.SetDataValue(pcLow);
            S--;

            PC = operandAddr;

        }
        private void PLP(ref sbyte operand) {
            S += 1;
            bus_.SetAddressValue((ushort)(0x100 + S));
            P = (sbyte)(bus_.GetDataValue() & ~0x30);

        }
        private void RTI(ref sbyte operand) {
            S += 1;
            P = bus_.Read((ushort)(0x100 + S));
          
            S += 1;
            byte addrLow = (byte)bus_.Read((ushort)(0x100 + S));
            S += 1;
            ushort addrHigh = (byte)bus_.Read((ushort)(0x100 + S));
            addrHigh <<= 8;
            PC = (ushort)(addrHigh + addrLow);
        }
        private void RTS(ref sbyte operand) {
            S++;
            bus_.SetAddressValue((ushort)(0x100+S));
            byte addrLow = (byte)bus_.GetDataValue();

            S++;
            bus_.SetAddressValue((ushort)(0x100 + S));
            ushort addrHigh = (ushort)(byte)bus_.GetDataValue();

            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh);
            PC = (ushort)(operandAddr + 1);

        }
        private void SBC(ref sbyte operand) {
            sbyte tmpVar = A;
            short res = (short)((byte)A - (byte)operand - (IsFlagSet(ProcessStatusFlags.C) ? 0x00: 0x01));
            A = (sbyte)(byte)res;
            ToggleFlag(ProcessStatusFlags.C, !(res < 0));
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ tmpVar) & (A ^ ~operand) & 0x80) == 0x80);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void STA(ref sbyte operand) { 
            bus_.Write(addrBuffer, A);
        }
        private void STX(ref sbyte operand) { bus_.Write(addrBuffer, X);}
        private void STY(ref sbyte operand) { bus_.Write(addrBuffer, Y);}

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

        /*==================================== Addressing modes =======================================*/
        private ref sbyte Abs()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)bus_.GetDataValue();
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)(byte)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh);
            bus_.SetAddressValue(operandAddr);
            PC += 1;
            addrBuffer = operandAddr;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 
        private ref sbyte AbsIndxX()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)(bus_.GetDataValue() );
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (byte)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh + (byte)X);
            addrBuffer = operandAddr;
            bus_.SetAddressValue(operandAddr);
            PC += 1;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte AbsIndxY()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)(bus_.GetDataValue());
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (byte)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh + (byte)Y);
            addrBuffer = operandAddr;
            bus_.SetAddressValue(operandAddr);
            PC += 1;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte AbsInd()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)bus_.GetDataValue();
            bool isEnd = addrLow == 0xFF;
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh);

            bus_.SetAddressValue(operandAddr);
            addrLow = (byte)bus_.GetDataValue();
            if (isEnd) operandAddr -= 0xFF;
            else operandAddr += 1;
            bus_.SetAddressValue(operandAddr);
            addrHigh = (byte)bus_.GetDataValue();

            PC = (ushort)((ushort)addrLow + ((ushort)addrHigh << 8));
            addrBuffer = PC;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ACC()
        {
            usesAcc = true;
            return ref A;
        } 

        private ref sbyte IMM()
        {
            PC += 1;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 


        private ref sbyte IMP()
        {
            return ref bus_.GetDataValue();
        } 

        private ref sbyte PCR()
        {
            sbyte offset = bus_.GetDataValue();
            PC += 1;
            byte pcHigh = (byte)((PC >> 8) & 0xFF);
            addrBuffer = (ushort)(PC + offset);
            usesAcc = false;
            required_clock_cycles += pcHigh == (0xFF & (PC >> 8)) ? 1 : 2;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte Stack()
        {
            ushort operandAddr = (ushort)(0x100 + S);
            bus_.SetAddressValue(operandAddr);
            addrBuffer = operandAddr;
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZP()
        {
            byte location = (byte)bus_.GetDataValue();
            ushort operandAddr = location;
            addrBuffer = operandAddr;
            bus_.SetAddressValue(operandAddr);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPIndxInd()
        {
            byte location = (byte)bus_.GetDataValue();
            byte operandAddr = (byte)(location + (byte)X);
            bus_.SetAddressValue(operandAddr);
            ushort addrLow = (byte)bus_.GetDataValue();
            operandAddr += 1;
            bus_.SetAddressValue(operandAddr);
            ushort addrHigh = (byte)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            addrBuffer = (ushort)(addrHigh + addrLow);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue(); 
        } 

        private ref sbyte ZPIndxX()
        {
            sbyte location = bus_.GetDataValue();
            byte operandAddr = (byte)((byte)location + (byte)X) ;
            bus_.SetAddressValue(operandAddr);
            addrBuffer = operandAddr;
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPIndxY()
        {
            sbyte location = bus_.GetDataValue();
            byte operandAddr = (byte)((byte)location + (byte)Y);
            addrBuffer = operandAddr;
            bus_.SetAddressValue(operandAddr);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPInd()
        {
            sbyte location = bus_.GetDataValue();
            sbyte operandAddr = (sbyte)location;
            bus_.SetAddressValue((ushort)operandAddr);
            bus_.SetAddressValue((ushort)operandAddr);
            ushort addrLow = (ushort)bus_.GetDataValue();
            operandAddr += 1;
            bus_.SetAddressValue((ushort)operandAddr);
            ushort addrHigh = (ushort)bus_.GetDataValue();
            PC = (ushort)(addrHigh << 8 + addrLow);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPIndIndxY()
        {
            byte zpAddr = (byte)bus_.GetDataValue();    // Indirect addr
            PC += 1;

            byte addrLow = (byte)bus_.Read(zpAddr);
            zpAddr += 1;
            ushort addrHigh = (ushort)((byte)bus_.Read(zpAddr) << 8);

            ushort operandAddr = (ushort)(addrLow + addrHigh + (byte)Y);
            bus_.SetAddressValue(operandAddr);
            addrBuffer = operandAddr;
            usesAcc = false;
            return ref bus_.GetDataValue();
        }


        /* =========================================== Instruction Execution Logic ======================================= */
        ExecutionInfo DecodeNextInstruction()
        {
            bus_.SetAddressValue(PC);
            byte opCode = (byte)bus_.GetDataValue();
            PC += 1;
            bus_.SetAddressValue(PC);
            byte mask = 0b00001111;
            int row = (opCode & ~mask) >> 4;
            int col = (opCode & mask);
            return OpCodeMatrix[row][col];
        }

        void ExecuteInstruction(ExecutionInfo instr)
        {
            // TODO: Account for clock cycles
            var instruction = instr.instruction;
            var addressingMode = instr.addressingMode;
            required_clock_cycles = instr.Cycles;
            instruction(ref addressingMode());
        }
        
        public void Run()
        {
            while(CPU_on)
            {
                var instruction = DecodeNextInstruction();
                ExecuteInstruction(instruction);
            }
        }

        public void RunTest()
        {
            while (CPU_on)
            {
                if(ready)
                {
                    var instruction = DecodeNextInstruction();
                    ExecuteInstruction(instruction);
                    //if (PC == 0xE104) Debugger.Break();
                    if (!testStarted)
                    {
                        byte testStatus = (byte)bus_.Read((ushort)0x6000);
                        testStarted = (testStatus == 0x80);
                    }
                    else
                    {
                        byte testStatus = (byte)bus_.Read((ushort)0x6000);
                        if (testStatus != 0x80) return;
                    }
                    ready = false;
                }
                

            }

        }

        public void Tick()
        {
            clocksTicks += 1;
            clocksTicks %= CLOCK_TICKS_MAX;
            if(clocksTicks == 0)
            {
                required_clock_cycles -= 1;
                ready = required_clock_cycles <= 0;
            }
            
        }
        public void KillCPU()
        {
            CPU_on = false;
        }

    }


}
