using System.Diagnostics;

namespace _6502Clone
{
    delegate void Tick();
    class SysClock
    {
        private readonly List<Tick> tickCallbacks;
        private readonly Stopwatch timer;
        private bool active;
        int interval;
        public SysClock()
        {
            active = true;
            timer = new Stopwatch();
            tickCallbacks = [];
            //interval = (int)(Stopwatch.Frequency / 9789773);
            interval = (int)(Stopwatch.Frequency * 0.001);
        }

        public void RegisterForTicks(Tick tickCallback)
        {
            tickCallbacks.Add(tickCallback);
        }

        public void RunClock()
        {
            timer.Start();
            while(active)
            {
                if(timer.ElapsedTicks > interval)
                {
                    foreach (Tick tick in tickCallbacks) { tick.Invoke(); }
                    timer.Restart();
                }
            }
            
        }
        public void Finish()
        {
            active = false;
        }

    }
}
