using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PatataStudio
{
    public abstract class JobScheduler
    {
        private readonly Queue<long> timings;
        private readonly Stopwatch watch;
        private readonly int records;

        protected JobScheduler(int records = 16)
        {
            this.records = records;
            watch = new Stopwatch();
            timings = new Queue<long>(this.records);
        }

        public float AvgTime => (float)timings.Sum() / 10;

        protected void StartRecord()
        {
            watch.Restart();
        }

        protected void StopRecord()
        {
            watch.Stop();
            var ms = watch.ElapsedMilliseconds;

            if (timings.Count <= records)
            {
                timings.Enqueue(ms);
            }
            else
            {
                timings.Dequeue();
                timings.Enqueue(ms);
            }
        }
    }
}