using uint_16 = System.UInt16;
using ExternalBus = _6502Clone.Bus.Bus;

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
        bool reachedTarget;
        ushort breakPt;

        bool usesAcc;
        public CPU(ref ExternalBus bus)
        {
            reachedTarget = false;
            breakPt = 0xE0E6;


            bus_ = bus;
            X = 0; Y = 0; S = 0; A = 0; P = 0;
            PC = 0;
            usesAcc = false;
            P ^= (sbyte)ProcessStatusFlags.I;
            OpCodeMatrix = [
                [new(BRK, Stack, 0), new(ORA, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ORA, ZP, 0), new(ASL, ZP, 0), new(NOP, IMP, 0), new(PHP, Stack, 0), new(ORA, IMM, 0), new(ASL, ACC, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ORA, Abs, 0), new(ASL, Abs, 0), new(NOP, IMP, 0)],
                [new(BPL, PCR, 0), new(ORA, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ORA, ZPIndxX, 0), new(ASL, ZPIndxX, 0), new(NOP, IMP, 0), new(CLC, IMP, 0), new(ORA, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ORA, AbsIndxX, 0), new(ASL, AbsIndxX, 0), new(NOP, IMP, 0)],
                [new(JSR, IMP, 0), new(AND, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(BIT, ZP, 0), new(AND, ZP, 0), new(ROL, ZP, 0), new(NOP, IMP, 0), new(PLP, Stack, 0), new(AND, IMM, 0), new(ROL, ACC, 0), new(NOP, IMP, 0), new(BIT, Abs, 0), new(AND, Abs, 0), new(ROL, Abs, 0), new(NOP, IMP, 0)],
                [new(BMI, PCR, 0), new(AND, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(AND, ZPIndxX, 0), new(ROL, ZPIndxX, 0), new(NOP, IMP, 0), new(SEC, IMP, 0), new(AND, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(AND, AbsIndxX, 0), new(ROL, AbsIndxX, 0), new(NOP, IMP, 0)],

                [new(RTI, Stack, 0), new(EOR, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(EOR, ZP, 0), new(LSR, ZP, 0), new(NOP, IMP, 0), new(PHA, Stack, 0), new(EOR, IMM, 0), new(LSR, ACC, 0), new(NOP, IMP, 0), new(JMP, Abs, 0), new(EOR, Abs, 0), new(LSR, Abs, 0), new(NOP, IMP, 0)],
                [new(BVC, PCR, 0), new(EOR, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(EOR, ZPIndxX, 0), new(LSR, ZPIndxX, 0), new(NOP, IMP, 0), new(CLI, IMP, 0), new(EOR, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(EOR, AbsIndxX, 0), new(LSR, AbsIndxX, 0), new(NOP, IMP, 0)],
                [new(RTS, IMP, 0), new(ADC, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ADC, ZP, 0), new(ROR, ZP, 0), new(NOP, IMP, 0), new(PLA, Stack, 0), new(ADC, IMM, 0), new(ROR, ACC, 0), new(NOP, IMP, 0), new(JMP, AbsInd, 0), new(ADC, Abs, 0), new(ROR, Abs, 0), new(NOP, IMP, 0)],
                [new(BVS, PCR, 0), new(ADC, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ADC, ZPIndxX, 0), new(ROR, ZPIndxX, 0), new(NOP, IMP, 0), new(SEI, IMP, 0), new(ADC, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(ADC, AbsIndxX, 0), new(ROR, AbsIndxX, 0), new(NOP, IMP, 0)],
                
                [new(NOP, IMP, 0), new(STA, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(STY, ZP, 0), new(STA, ZP, 0), new(STX, ZP, 0), new(NOP, IMP, 0), new(DEY, IMP, 0), new(NOP, IMP, 0), new(TXA, IMP, 0), new(NOP, IMP, 0), new(STY, Abs, 0), new(STA, Abs, 0), new(STX, Abs, 0), new(NOP, IMP, 0)],
                [new(BCC, PCR, 0), new(STA, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(STY, ZPIndxX, 0), new(STA, ZPIndxX, 0), new(STX, ZPIndxY, 0), new(NOP, IMP, 0), new(TYA, IMP, 0), new(STA, AbsIndxY, 0), new(TXS, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(STA, AbsIndxX, 0), new(NOP, IMP, 0), new(NOP, IMP, 0)],
                [new(LDY, IMM, 0), new(LDA, ZPIndxX, 0), new(LDX, IMM, 0), new(NOP, IMP, 0), new(LDY, ZP, 0), new(LDA, ZP, 0), new(LDX, ZP, 0), new(NOP, IMP, 0), new(TAY, IMP, 0), new(LDA, IMM, 0), new(TAX, IMP, 0), new(NOP, IMP, 0), new(LDY, Abs, 0), new(LDA, Abs, 0), new(LDX, Abs, 0), new(NOP, IMP, 0)],
                [new(BCS, PCR, 0), new(LDA, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(LDY, ZPIndxX, 0), new(LDA, ZPIndxX, 0), new(LDX, ZPIndxY, 0), new(NOP, IMP, 0), new(CLV, IMP, 0), new(LDA, AbsIndxY, 0), new(TSX, IMP, 0), new(NOP, IMP, 0), new(LDY, AbsIndxX, 0), new(LDA, AbsIndxX, 0), new(LDX, AbsIndxY, 0), new(NOP, IMP, 0)],
     
                [new(CPY, IMM, 0), new(CMP, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(CPY, ZP, 0), new(CMP, ZP, 0), new(DEC, ZP, 0), new(NOP, IMP, 0), new(INY, IMP, 0), new(CMP, IMM, 0), new(DEX, IMP, 0), new(NOP, IMP, 0), new(CPY, Abs, 0), new(CMP, Abs, 0), new(DEC, Abs, 0), new(NOP, IMP, 0)],
                [new(BNE, PCR, 0), new(CMP, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(CMP, ZPIndxX, 0), new(DEC, ZPIndxX, 0), new(NOP, IMP, 0), new(CLD, IMP, 0), new(CMP, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(CMP, AbsIndxX, 0), new(DEC, AbsIndxX, 0), new(NOP, IMP, 0)],
                [new(CPX, IMM, 0), new(SBC, ZPIndxInd, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(CPX, ZP, 0), new(SBC, ZP, 0), new(INC, ZP, 0), new(NOP, IMP, 0), new(INX, IMP, 0), new(SBC, IMM, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(CPX, Abs, 0), new(SBC, Abs, 0), new(INC, Abs, 0), new(NOP, IMP, 0)],
                [new(BEQ, PCR, 0), new(SBC, ZPIndIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(SBC, ZPIndxX, 0), new(INC, ZPIndxX, 0), new(NOP, IMP, 0), new(SED, IMP, 0), new(SBC, AbsIndxY, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(NOP, IMP, 0), new(SBC, AbsIndxX, 0), new(INC, AbsIndxX, 0), new(NOP, IMP, 0)],

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
            int result = (A + operand + (IsFlagSet(ProcessStatusFlags.C) ? (sbyte)0x01 : (sbyte)0));
            A = (sbyte)(byte)result;
            ToggleFlag(ProcessStatusFlags.C, result > 0XFF);
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, (tmpVar * operand)  < 0);
            ToggleFlag(ProcessStatusFlags.N, (A & 0x80) == 0x80);
        }
        private void AND(ref sbyte operand) {
            A &= operand;
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.N,  (A & 0x80) == 0x80);
        }
        private void ASL(ref sbyte operand) {
            ToggleFlag(ProcessStatusFlags.C, (operand & 0x80) == 0x80);
            operand <<= 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 0x80);
        }
        private void BCC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.C)) PC = addrBuffer;}
        private void BCS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.C)) PC = addrBuffer;}
        private void BEQ(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.Z)) PC = addrBuffer;}
        private void BMI(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.N)) PC = addrBuffer;}
        private void BNE(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.Z)) PC = addrBuffer;}
        private void BPL(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.N)) PC = addrBuffer;}
        private void BVC(ref sbyte operand) {if(!IsFlagSet(ProcessStatusFlags.V)) PC = (ushort)(addrBuffer + 2);}
        private void BVS(ref sbyte operand) {if(IsFlagSet(ProcessStatusFlags.V)) PC = addrBuffer;}

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
            operand += 1;
            ToggleFlag(ProcessStatusFlags.Z, operand == 0);
            ToggleFlag(ProcessStatusFlags.N, (operand & 0x80) == 0x80);
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
            ToggleFlag(ProcessStatusFlags.V, (result & 0x70) == 0x70);
            ToggleFlag(ProcessStatusFlags.N, (result & 0x80) == 0x80);
        }
        private void BRK(ref sbyte operand) {}
        private void JMP(ref sbyte operand) {
            if ((0xE47E <= addrBuffer && addrBuffer <= 0xE617)) return;
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
            if(operandAddr == 0xE800 || (0xE47E <= operandAddr && operandAddr <= 0xE617))   // Disable subroutines targetting the ppu until its implemented, these cause the CPU to hang
            {
                S += 2;
                PC++;
                return;
            }

            PC = operandAddr;

        }
        private void PLP(ref sbyte operand) {
            S += 1;
            bus_.SetAddressValue((ushort)(0x100 + S));
            P = (sbyte)(bus_.GetDataValue() & ~0x30);

        }
        private void RTI(ref sbyte operand) {}
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
            A = (sbyte)(A - operand - ~(int)ProcessStatusFlags.C & 1);
            ToggleFlag(ProcessStatusFlags.C, !(A < 0x00));
            ToggleFlag(ProcessStatusFlags.Z, A == 0);
            ToggleFlag(ProcessStatusFlags.V, ((A ^ A) & (A ^ ~operand) & 0x80) == 0);
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

        private ref sbyte AbsIndxIndX()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)(bus_.GetDataValue() + (byte)X);
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)bus_.GetDataValue();
            ushort operandAddr = (ushort)(addrLow + addrHigh << 8);
            bus_.SetAddressValue(operandAddr);
            addrLow = (byte)bus_.GetDataValue();
            operandAddr += 1;
            bus_.SetAddressValue(operandAddr);
            addrHigh = (byte)bus_.GetDataValue();

            PC = (ushort)((ushort)addrLow + ((ushort)addrHigh << 8));
            usesAcc = false;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte AbsIndxX()
        {
            bus_.SetAddressValue(PC);
            byte addrLow = (byte)(bus_.GetDataValue() );
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)bus_.GetDataValue();
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
            ushort addrHigh = (ushort)bus_.GetDataValue();
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
            PC += 1;
            bus_.SetAddressValue(PC);
            ushort addrHigh = (ushort)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            ushort operandAddr = (ushort)(addrLow + addrHigh);
            bus_.SetAddressValue(operandAddr);
            addrLow = (byte)bus_.GetDataValue();
            operandAddr += 1;
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
            addrBuffer = (ushort)(PC + offset);
            usesAcc = false;
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
            sbyte location = bus_.GetDataValue();
            ushort operandAddr = (ushort)location;
            addrBuffer = operandAddr;
            bus_.SetAddressValue(operandAddr);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPIndxInd()
        {
            sbyte location = bus_.GetDataValue();
            sbyte operandAddr = (sbyte)(location + (byte)X);
            bus_.SetAddressValue((ushort)operandAddr);
            ushort addrLow = (ushort)bus_.GetDataValue();
            operandAddr += 1;
            bus_.SetAddressValue((ushort)operandAddr);
            ushort addrHigh = (ushort)bus_.GetDataValue();
            addrHigh = (ushort)(addrHigh << 8);
            PC = (ushort)(addrHigh + addrLow);
            usesAcc = false;
            addrBuffer = PC;
            return ref bus_.GetDataValue(); 
        } 

        private ref sbyte ZPIndxX()
        {
            sbyte location = bus_.GetDataValue();
            sbyte operandAddr = (sbyte)(location + (byte)X);
            bus_.SetAddressValue((ushort)operandAddr);
            usesAcc = false;
            PC += 1;
            return ref bus_.GetDataValue();
        } 

        private ref sbyte ZPIndxY()
        {
            sbyte location = bus_.GetDataValue();
            sbyte operandAddr = (sbyte)(location + (byte)Y);
            addrBuffer = (ushort)operandAddr;
            bus_.SetAddressValue((ushort)operandAddr);
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
            ushort zpAddr = (ushort)bus_.GetDataValue();
            PC += 1;
            ushort operandAddr = (ushort)(zpAddr + Y);
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
            instruction(ref addressingMode());
        }
        
        public void Run()
        {
            while(true)
            {
                var instruction = DecodeNextInstruction();
                ExecuteInstruction(instruction);
            }
        }

        public void RunTest()
        {
            while (true)
            {
                var instruction = DecodeNextInstruction();
                ExecuteInstruction(instruction);
                sbyte testStatus = bus_.Read((ushort)0x6000);
                if(PC == (ushort)0xE815)
                {
                    System.Diagnostics.Debugger.Break();
                }
                //else if(PC == breakPt && reachedTarget)
                //{
                //    System.Diagnostics.Debugger.Break();
                //}
                
            }

        }
    }

}
