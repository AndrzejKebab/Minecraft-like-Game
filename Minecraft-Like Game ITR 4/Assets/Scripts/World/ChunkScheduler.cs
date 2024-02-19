using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UtilityLibrary.Unity.Runtime.Values;

namespace PatataStudio.World
{
    public class ChunkScheduler : JobScheduler
    {
        private static readonly int3 chunkSize = new(32, 32, 32);

        private readonly ChunkManager chunkManager;
        //private NoiseProfile _NoiseProfile;

        private JobHandle handle;

        private NativeList<int3> jobs;
        private NativeParallelHashMap<int3, Chunk> results;

        public ChunkScheduler(ChunkManager chunkManager)
        {
            this.chunkManager = chunkManager;

            jobs = new NativeList<int3>(Allocator.Persistent);
            results = new NativeParallelHashMap<int3, Chunk>(
                GameSettings.ViewDistance.CubedSize(),
                Allocator.Persistent
            );
        }

        internal bool IsReady = true;
        internal bool IsComplete => handle.IsCompleted;

        internal void Start(List<int3> jobs)
        {
            StartRecord();

            IsReady = false;

            foreach (var job in jobs) this.jobs.Add(job);

            var chunkJob = new ChunkJob
            {
                Jobs = this.jobs,
                ChunkSize = chunkSize,
                Results = results.AsParallelWriter()
            };

            handle = chunkJob.Schedule(this.jobs.Length, 1);
        }

        internal void Complete()
        {
            handle.Complete();

            chunkManager.AddChunks(results);

            jobs.Clear();
            results.Clear();

            IsReady = true;
            StopRecord();
        }

        internal void Dispose()
        {
            handle.Complete();

            jobs.Dispose();
            results.Dispose();
        }
    }
}