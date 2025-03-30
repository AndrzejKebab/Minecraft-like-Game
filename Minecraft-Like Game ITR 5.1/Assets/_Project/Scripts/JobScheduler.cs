using System;
using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace PatataGames;

/// <summary>
/// Base job scheduler that provides common functionality for managing Unity job handles.
/// Handles batched completion of jobs with yielding to prevent main thread blocking.
/// </summary>
[BurstCompile]
public struct JobSchedulerBase(int initialCapacity = 64) : IDisposable
{
    private NativeList<JobHandle> jobHandles = new(initialCapacity, Allocator.Persistent);
    
    /// <summary>
    /// Controls how many jobs are processed before yielding back to the main thread.
    /// Default is 8.
    /// </summary>
    public byte BatchSize { get; set; } = 8;

    /// <summary>
    /// Adds a job handle to the tracking list.
    /// </summary>
    /// <param name="handle">The job handle to track.</param>
    public void ScheduleJob(JobHandle handle)
    {
        jobHandles.Add(handle);
    }

    /// <summary>
    /// Completes all tracked jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are finished.</returns>
    [BurstCompile]
    public async UniTask Complete()
    {
        byte count = 0;
        using var completed = new NativeList<int>(Allocator.Temp);

        for (var i = 0; i < jobHandles.Length; i++)
        {
            count++;
            jobHandles[i].Complete();
            completed.Add(i);

            if (count < BatchSize) continue;
            await UniTask.Yield();
            count = 0;
        }

        for (var i = completed.Length - 1; i >= 0; i--) jobHandles.RemoveAt(completed[i]);

        if (count > 0) await UniTask.Yield();
    }

    /// <summary>
    /// Completes all tracked jobs without yielding.
    /// Use this when immediate completion is required.
    /// </summary>
    [BurstCompile]
    public void CompleteAll()
    {
        for (var i = 0; i < jobHandles.Length; i++) jobHandles[i].Complete();
        jobHandles.Clear();
    }

    /// <summary>
    /// Completes all jobs and releases resources.
    /// </summary>
    public void Dispose()
    {
        CompleteAll();
        jobHandles.Dispose();
    }
    
    /// <summary>
    /// Returns the number of tracked job handles.
    /// </summary>
    /// <returns>The count of job handles currently being tracked.</returns>
    public int GetJobHandlesCount() => jobHandles.Length;
}

/// <summary>
/// Specialized scheduler for IJob implementations.
/// Provides batched scheduling and completion of jobs with yielding to prevent main thread blocking.
/// </summary>
/// <typeparam name="T">The job type, which must be an unmanaged struct implementing IJob.</typeparam>
[BurstCompile]
public struct JobScheduler<T>(int initialCapacity = 64) : IDisposable
    where T : unmanaged, IJob
{
    private JobSchedulerBase baseScheduler = new(initialCapacity);
    private NativeList<T>    jobQueue      = new(initialCapacity, Allocator.Persistent);

    /// <summary>
    /// Controls how many jobs are processed before yielding back to the main thread.
    /// Default is 8.
    /// </summary>
    public byte BatchSize
    {
        get => baseScheduler.BatchSize;
        set => baseScheduler.BatchSize = value;
    }
    
    /// <summary>
    /// Adds a job to the queue for scheduling.
    /// </summary>
    /// <param name="job">The job to add.</param>
    [BurstCompile]
    public void AddJob(T job)
    {
        jobQueue.Add(job);
    }
    
    /// <summary>
    /// Adds an external job handle to the tracking list.
    /// </summary>
    /// <param name="handle">The job handle to track.</param>
    [BurstCompile]
    public void ScheduleJob(JobHandle handle)
    {
        baseScheduler.ScheduleJob(handle);
    }

    /// <summary>
    /// Schedules all queued jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are scheduled.</returns>
    [BurstCompile]
    public async UniTask ScheduleAll()
    {
        byte count = 0;

        foreach (T job in jobQueue)
        {
            count++;
            JobHandle handle = job.Schedule();
            baseScheduler.ScheduleJob(handle);

            if (count < BatchSize) continue;
            await UniTask.Yield();
            count = 0;
        }

        jobQueue.Clear();

        if (count > 0) await UniTask.Yield();
    }

    /// <summary>
    /// Completes all tracked jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are finished.</returns>
    [BurstCompile]
    public UniTask Complete() => baseScheduler.Complete();

    /// <summary>
    /// Completes all tracked jobs without yielding.
    /// Use this when immediate completion is required.
    /// </summary>
    [BurstCompile]
    public void CompleteAll() => baseScheduler.CompleteAll();

    /// <summary>
    /// Completes all jobs and releases resources.
    /// </summary>
    public void Dispose()
    {
        baseScheduler.Dispose();
        jobQueue.Dispose();
    }
}

/// <summary>
/// Specialized scheduler for IJobFor implementations.
/// Provides batched scheduling and completion of jobs with yielding to prevent main thread blocking.
/// </summary>
/// <typeparam name="T">The job type, which must be an unmanaged struct implementing IJobFor.</typeparam>
[BurstCompile]
public struct JobForScheduler<T>(int initialCapacity = 64) : IDisposable
    where T : unmanaged, IJobFor
{
    private JobSchedulerBase       baseScheduler = new(initialCapacity);
    private NativeList<JobForData> jobQueue      = new(initialCapacity, Allocator.Persistent);

    /// <summary>
    /// Controls how many jobs are processed before yielding back to the main thread.
    /// Default is 8.
    /// </summary>
    public byte BatchSize
    {
        get => baseScheduler.BatchSize;
        set => baseScheduler.BatchSize = value;
    }
    
    /// <summary>
    /// Internal structure to store job data along with its array length.
    /// </summary>
    [BurstCompile]
    private struct JobForData
    {
        public T Job;
        public int ArrayLength;
    }

    /// <summary>
    /// Adds a job to the queue for scheduling with the specified array length.
    /// </summary>
    /// <param name="job">The job to add.</param>
    /// <param name="arrayLength">The length of the array to process.</param>
    [BurstCompile]
    public void AddJob(T job, int arrayLength)
    {
        jobQueue.Add(new JobForData
        {
            Job = job,
            ArrayLength = arrayLength
        });
    }

    /// <summary>
    /// Adds an external job handle to the tracking list.
    /// </summary>
    /// <param name="handle">The job handle to track.</param>
    [BurstCompile]
    public void ScheduleJob(JobHandle handle)
    {
        baseScheduler.ScheduleJob(handle);
    }

    /// <summary>
    /// Schedules all queued jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are scheduled.</returns>
    [BurstCompile]
    public async UniTask ScheduleAll()
    {
        byte count = 0;

        foreach (JobForData data in jobQueue)
        {
            count++;
            JobHandle  handle  = data.Job.Schedule(data.ArrayLength, default);
            baseScheduler.ScheduleJob(handle);

            if (count < BatchSize) continue;
            await UniTask.Yield();
            count = 0;
        }

        jobQueue.Clear();

        if (count > 0) await UniTask.Yield();
    }

    /// <summary>
    /// Completes all tracked jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are finished.</returns>
    [BurstCompile]
    public UniTask Complete() => baseScheduler.Complete();

    /// <summary>
    /// Completes all tracked jobs without yielding.
    /// Use this when immediate completion is required.
    /// </summary>
    [BurstCompile]
    public void CompleteAll() => baseScheduler.CompleteAll();

    /// <summary>
    /// Completes all jobs and releases resources.
    /// </summary>
    public void Dispose()
    {
        baseScheduler.Dispose();
        jobQueue.Dispose();
    }
}

/// <summary>
/// Specialized scheduler for IJobParallelFor implementations.
/// Provides batched scheduling and completion of jobs with yielding to prevent main thread blocking.
/// </summary>
/// <typeparam name="T">The job type, which must be an unmanaged struct implementing IJobParallelFor.</typeparam>
[BurstCompile]
public struct JobParallelForScheduler<T>(int initialCapacity = 64) : IDisposable
    where T : unmanaged, IJobParallelFor
{
    private JobSchedulerBase               baseScheduler = new(initialCapacity);
    private NativeList<JobParallelForData> jobQueue      = new(initialCapacity, Allocator.Persistent);

    /// <summary>
    /// Controls how many jobs are processed before yielding back to the main thread.
    /// Default is 8.
    /// </summary>
    public byte BatchSize
    {
        get => baseScheduler.BatchSize;
        set => baseScheduler.BatchSize = value;
    }
    
    /// <summary>
    /// Internal structure to store job data along with its array length and inner batch size.
    /// </summary>
    [BurstCompile]
    private struct JobParallelForData
    {
        public T Job;
        public int ArrayLength;
        public int InnerBatchSize;
    }
    
    /// <summary>
    /// Adds a job to the queue for scheduling with the specified array length and inner batch size.
    /// </summary>
    /// <param name="job">The job to add.</param>
    /// <param name="arrayLength">The length of the array to process.</param>
    /// <param name="innerBatchSize">The batch size for each worker thread. Default is 64.</param>
    [BurstCompile]
    public void AddJob(T job, int arrayLength, int innerBatchSize = 64)
    {
        jobQueue.Add(new JobParallelForData
        {
            Job = job,
            ArrayLength = arrayLength,
            InnerBatchSize = innerBatchSize
        });
    }
    
    /// <summary>
    /// Adds an external job handle to the tracking list.
    /// </summary>
    /// <param name="handle">The job handle to track.</param>
    [BurstCompile]
    public void ScheduleJob(JobHandle handle)
    {
        baseScheduler.ScheduleJob(handle);
    }

    /// <summary>
    /// Schedules all queued jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are scheduled.</returns>
    [BurstCompile]
    public async UniTask ScheduleAll()
    {
        byte count = 0;

        foreach (JobParallelForData data in jobQueue)
        {
            count++;
            JobHandle          handle  = data.Job.Schedule(data.ArrayLength, data.InnerBatchSize);
            baseScheduler.ScheduleJob(handle);

            if (count < BatchSize) continue;
            await UniTask.Yield();
            count = 0;
        }

        jobQueue.Clear();

        if (count > 0) await UniTask.Yield();
    }

    /// <summary>
    /// Completes all tracked jobs in batches, yielding between batches to prevent
    /// blocking the main thread for too long.
    /// </summary>
    /// <returns>A UniTask that completes when all jobs are finished.</returns>
    [BurstCompile]
    public UniTask Complete() => baseScheduler.Complete();

    /// <summary>
    /// Completes all tracked jobs without yielding.
    /// Use this when immediate completion is required.
    /// </summary>
    [BurstCompile]
    public void CompleteAll() => baseScheduler.CompleteAll();

    /// <summary>
    /// Completes all jobs and releases resources.
    /// </summary>
    public void Dispose()
    {
        baseScheduler.Dispose();
        jobQueue.Dispose();
    }
}