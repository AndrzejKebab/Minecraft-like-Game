using UtilityLibrary.Unity.Runtime.PriorityQueue;

namespace PatataStudio
{
    public class JobQueue<T>
    {
        private SimplePriorityQueue<T, float> claimQueue;
        private SimplePriorityQueue<T, float> reclaimQueue;

        public JobQueue()
        {
            claimQueue = new SimplePriorityQueue<T, float>();
            reclaimQueue = new SimplePriorityQueue<T, float>();
        }

        public void Schedule() { }
        public void Process() { }
        public void Complete() { }
        public void Clear() { }
    }
}