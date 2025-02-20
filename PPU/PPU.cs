using System.Diagnostics;
using _6502Clone;
using _6502Clone.Bus;
using SFML.System;
using SFML.Window;
using SFML.Graphics;

namespace _6502
{
    class PPU
    {
        bool active;

        private int clocksTicks;
        private const int CLOCK_TICKS_MAX = 1;
        private bool ready;
        private int current_draw_counts;
        private const int CYCLES_PER_DRAW = 8932;

        // VRAM and registers
        private byte[] VRAM;
        private readonly byte[] registers;

        // Handle writes to the ppu addr
        private ushort PPUADDRVal;
        bool onLowByte;

        // Handle writes to the scroll buffer
        private int x_scroll;
        private int y_scroll;
        bool onX;

        // Define register names
        private const int PPUCONTROL = 0x0000;
        private const int PPUMASK = 0x0001;
        private const int PPUSTATUS = 0x0002;
        private const int OAMADDR = 0x0003;
        private const int OAMDATA = 0x0004;
        private const int PPUSCROLL = 0x0005;
        private const int PPUADDR = 0x0006;
        private const int PPUDATA = 0x0007;
        private const int OAMDMA = 0x0014;
        private const int REGISTER_COUNT = 0x08;

        // Master color pelette
        private Dictionary<byte, Color> ColorMap;
        private const string ColorPelettePath = "D:\\nes\\6502Clone\\PPU\\colors.pal";

        // Define display properties
        private RenderWindow screen;
        private const int DISPLAY_SCALE_FACTOR = 3;
        private int current_row;
        private int current_col;

        private const int ROW_LEN = 240;
        private const int COL_LEN = 256;
        private Bus bus_;

        // Testing stuff
        private byte TileIndex;
        private int tmp = 0;

        public PPU(ref Bus bus, ref SysClock clock)
        {
            screen = new RenderWindow(new VideoMode(), "Testing");
            screen.Close();
            onX = true;
            TileIndex = 0;
            ColorMap = [];
            bus_ = bus;
            VRAM = new byte[0x4000];
            current_col = 0;
            current_row = 0;
            current_draw_counts = 0;
   
            clock.RegisterForTicks(Tick);
            clocksTicks = 0;
            ready = false;
            active = true;
            registers = new byte[8]; // Exposed to the CPU
            bus.RegisterForReads(Read);
            bus.RegisterForWrites(Write);
            LoadColors();
            onLowByte = false;
            PPUADDRVal = 0;
        }

        public void CloseDisplay(object? sender, EventArgs e)
        {
            Console.WriteLine("Closing Game Window");
            Finish();
        }

        public byte? Read(ushort addr)
        {
            if(0x2000 <= addr && addr < 0x3FFF)
            {
                return registers[(addr - 0x2000) % 8];
            }
            return null;
        }

        public void Write(ushort addr, sbyte data)
        {
            if(0x2000 <= addr && addr < 0x4000)
            {
                if (addr == 0x2000 + PPUADDR)
                {
                    WritePPUAddr((byte)data);
                    return;
                }
                else if (addr == 0x2000 + PPUDATA)
                {
                    WritePPUData((byte)data);
                    return;
                }
                else if (addr == 0x2000 + PPUSCROLL)
                {
                    WriteScrollData((byte)data);
                    return;
                }

                int index = (addr - 0x2000) % REGISTER_COUNT;
                registers[index] = (byte)data;
            }
        }

        private void WritePPUAddr(byte data)
        {
            if(onLowByte)
            {
                onLowByte = false;
                PPUADDRVal &= 0xFF00;
                PPUADDRVal |= data;
            }
            else
            {
                onLowByte = true;
                PPUADDRVal &= 0x00FF;
                ushort writeData = (ushort)(data & ~0xC0);
                writeData <<= 8;
                PPUADDRVal |= writeData;
            }
        }

        private void WritePPUData(byte data)
        {
            registers[PPUDATA] = data;
            VRAM[PPUADDRVal] = data;
            PPUADDRVal += (ushort)(((registers[PPUCONTROL] & 0x04) == 0) ? 1 : 32);
            PPUADDRVal = (ushort)(PPUADDRVal % 0x4000);
        }

        private void WriteScrollData(byte data) {
            if (onX) x_scroll = data;
            else y_scroll = data;
            onX = !onX;
        }

        public void RunPPU()
        {
            screen.Clear(Color.White);
            while (active)
            {
                if (ready)
                {
                    
                    screen.DispatchEvents();
                    DrawBGTile();
                    ready = false;
                }
            }
        }
        public void Finish()
        {
            active = false;
        }

        public void Tick()
        {
            clocksTicks += 1;
            clocksTicks %= CLOCK_TICKS_MAX;
            ready = clocksTicks == 0;
            current_draw_counts++;
            current_draw_counts %= CYCLES_PER_DRAW;
            if (current_draw_counts == 0)
            {
                registers[PPUSTATUS] |= 0x80;
            }
        }

        public void InitDisplay()
        {

            screen = new RenderWindow(new VideoMode(256 * DISPLAY_SCALE_FACTOR, 240 * DISPLAY_SCALE_FACTOR), "MY NES EMULATOR");
            screen.Closed += CloseDisplay;
        }

        private void DrawBGTile()
        {
            if (current_col == (COL_LEN/8) - 1)
            {
                current_col = 0;
                if (current_row == (ROW_LEN/8) - 1)
                {
                    current_row = 0;
                    screen.Display();
                    screen.Clear();
                    return;
                }
                current_row += 1;
                return;
            }
            var tex = GenerateTile(current_row*32 + current_col);
            var tmp = new Sprite(tex);

            int scroll_x = x_scroll + ((registers[PPUCONTROL] & 0x01) == 0 ? 0 : 256);
            int scroll_y = y_scroll + ((registers[PPUCONTROL] & 0x02) == 0 ? 0 : 240);

            tmp.Position = new Vector2f(current_col * DISPLAY_SCALE_FACTOR * 8 + scroll_x * DISPLAY_SCALE_FACTOR, current_row * DISPLAY_SCALE_FACTOR * 8 + scroll_y * DISPLAY_SCALE_FACTOR);
            screen.Draw(tmp);
            current_col += 1;
            
        }
        private void LoadColors()
        {
            byte[] colorBytes = File.ReadAllBytes(ColorPelettePath);
            var colors = colorBytes.Chunk(3);
            byte colorIndex = 0x00;
            

            foreach (var color in colors)
            {
                ColorMap.Add(colorIndex, new Color(color[0], color[1], color[2], (byte)((colorIndex == 0) ? 0: 255)));
                colorIndex++;
                if (colorIndex == 54) return;
            }
            
        }

        private Texture GenerateTile(int index)
        {
            RenderTexture target = new(8*DISPLAY_SCALE_FACTOR, 8*DISPLAY_SCALE_FACTOR);
            int patternTableIndex = VRAM[0x2000 + index];
            for (int i = 0; i < 8; i++)
            {                
                byte LSB = (byte)bus_.PPU_Read((ushort)(16 * patternTableIndex + i));
                byte MSB = (byte)bus_.PPU_Read((ushort)(16 * patternTableIndex + i + 8));
                RenderTextureRow(MSB, LSB, 7 - i, ref target);
            }
            
            return target.Texture;
        }

        private void RenderTextureRow(byte MSB, byte LSB, int row, ref RenderTexture target)
        {
            
            for(byte i = 0; i < 8; i++)
            {
                RectangleShape pixel = new(new Vector2f(DISPLAY_SCALE_FACTOR, DISPLAY_SCALE_FACTOR));
                byte mask = (byte)(1 << (7 - i));
                int tileIndex = ((MSB & mask) != 0 ? 2 : 0) + ((LSB & mask) != 0 ? 1 : 0);
                if(tileIndex != 0)
                {
                    int paletteIndex = 0x3F00 + tileIndex;
                    byte colorkey = VRAM[paletteIndex];
                    Color color = ColorMap[colorkey];
                    pixel.FillColor = color;

                    pixel.Position = new Vector2f(i * DISPLAY_SCALE_FACTOR, row * DISPLAY_SCALE_FACTOR);
                    target.Draw(pixel);
                }
                
            }
        }
    }

}