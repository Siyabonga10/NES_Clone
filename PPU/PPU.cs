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
        private readonly byte[] registers;
        bool active;

        private int clocksTicks;
        private const int CLOCK_TICKS_MAX = 1;
        private bool ready;
        private int current_draw_counts;
        private const int CYCLES_PER_DRAW = 8932;

        // VRAM, stores nametables used for rendering the backgrounds and stuff
        private byte[] VRAM;
        private bool reading;

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

        // Master color pelette
        private Dictionary<byte, Color> ColorMap;
        private const string ColorPelettePath = "D:\\nes\\6502Clone\\PPU\\colors.pal";

        // Define display properties
        private RenderWindow screen;
        private const int DISPLAY_SCALE_FACTOR = 3;
        private int current_row;
        private int current_col;
        private RectangleShape tile;

        private const int ROW_LEN = 240;
        private const int COL_LEN = 256;
        private Bus bus_;

        private Stopwatch FPS_timer;

        // Testing stuff
        private byte TileIndex;
        private ushort TileByteAddr;

        public PPU(ref Bus bus, ref SysClock clock)
        {
            TileByteAddr = 0;
            TileIndex = 0;
            ColorMap = [];
            bus_ = bus;
            reading = false;
            VRAM = new byte[0x4000];
            tile = new(new Vector2f(DISPLAY_SCALE_FACTOR * 8.0f, DISPLAY_SCALE_FACTOR * 8.0f));
            tile.FillColor = new(0, 0, 0, 255);
            current_col = 0;
            current_row = 0;

            screen = new RenderWindow(new VideoMode(256*DISPLAY_SCALE_FACTOR, 240*DISPLAY_SCALE_FACTOR), "MY NES EMULATOR");
            screen.KeyPressed += CloseDisplay;
            current_draw_counts = 0;
   
            clock.RegisterForTicks(Tick);
            clocksTicks = 0;
            ready = false;
            active = true;
            registers = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]; // Exposed to the CPU
            bus.RegisterForReads(Read);
            FPS_timer = new Stopwatch();
            LoadColors();
            screen.SetActive(false); // Disable the current context, allows another thread to use window later

            // Manually define palletes for testing, MUST BE REMOVED
            VRAM[0x3F00] = 0x0F;
            VRAM[0x3F01] = 0x30;
            VRAM[0x3F02] = 0x30;
            VRAM[0x3F03] = 0x30;
        }

        private void CloseDisplay(object? sender, EventArgs e)
        {
            Console.WriteLine(e.ToString());
            //screen.Close();
            Console.WriteLine("Logger");
            //active = false;
        }

        public byte? Read(ushort addr)
        {
            if (reading) return null;
            if(0x2000 <= addr && addr < 0x3FFF)
            {
                return registers[(addr - 0x2000) % 8];
            }
            return null;
        }

        public void RunPPU()
        {
            FPS_timer.Start();
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
            screen.SetActive(true);
        }

        private void DrawBGTile()
        {
            if (current_col == (COL_LEN/8) - 1)
            {
                current_col = 0;
                if (current_row == (ROW_LEN/8) - 1)
                {
                    current_row = 0;
                    
                    
                    var tex = GenerateTile();
                    TileIndex += 1;
                    var tmp = new Sprite(tex);
                    
                    tmp.Position = new Vector2f(100, 100);
                    screen.Clear();
                    screen.Draw(tmp);
                    screen.Display();
                    Console.WriteLine("FPS: " + (1000.0 / FPS_timer.ElapsedMilliseconds).ToString());
                    FPS_timer.Restart();
                    return;
                }
                current_row += 1;
                return;
            }
            current_col += 1;
            
        }

        private sbyte ReadCatridgeData(ushort Addr)
        {
            reading = true;
            sbyte value = bus_.PPU_Read(Addr);
            reading = false;
            return value;
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

        private Texture GenerateTile()
        {
            RenderTexture target = new(8*DISPLAY_SCALE_FACTOR, 8*DISPLAY_SCALE_FACTOR);
            for(int i = 0; i < 8; i++)
            {
                byte LSB = (byte)bus_.PPU_Read((ushort)(16 * TileIndex + i));
                byte MSB = (byte)bus_.PPU_Read((ushort)(16 * TileIndex + i + 8));
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