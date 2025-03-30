using System;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using ZLinq;

namespace PatataGames;

// Interface for our job wrapper to handle different job types
public interface IJobWrapper
{
	public JobHandle Schedule(JobHandle dependency = default);
}

// Wrapper for IJob
public readonly struct JobWrapper<T>(T job) : IJobWrapper
	where T : unmanaged, IJob
{
	public JobHandle Schedule(JobHandle dependency = default)
	{
		return job.Schedule(dependency);
	}
}

// Wrapper for IJobFor
public readonly struct JobForWrapper<T>(T job, int arrayLength, int innerLoopBatchCount = 8) : IJobWrapper
	where T : unmanaged, IJobFor
{
	public JobHandle Schedule(JobHandle dependency = default)
	{
		return job.Schedule(arrayLength, dependency);
	}

	public JobHandle ScheduleParallel(JobHandle dependency = default)
	{
		return job.ScheduleParallel(arrayLength, innerLoopBatchCount, dependency);
	}
}

// Wrapper for IJobParallelFor
public readonly struct JobParallelForWrapper<T>(T job, int arrayLength, int innerLoopBatchCount = 8) : IJobWrapper
	where T : unmanaged, IJobParallelFor
{
	public JobHandle Schedule(JobHandle dependency = default)
	{
		return job.Schedule(arrayLength, innerLoopBatchCount, dependency);
	}
}

// The main JobScheduler that works with any job type through the wrapper
public struct JobScheduler() : IDisposable
{
	private NativeList<JobHandle>    jobHandles = new(Allocator.Persistent);
	private NativeQueue<IJobWrapper> jobQueue   = new(Allocator.Persistent);
	public  byte                     BatchSize { get; set; } = 8;

	// Add different job types using the appropriate wrapper
	public void AddJob<T>(T job) where T : unmanaged, IJob
	{
		jobQueue.Enqueue(new JobWrapper<T>(job));
	}

	public void AddJobFor<T>(T job, int arrayLength, int batchCount = 8) where T : unmanaged, IJobFor
	{
		jobQueue.Enqueue(new JobForWrapper<T>(job, arrayLength, batchCount));
	}

	public void AddJobParallelFor<T>(T job, int arrayLength, int batchCount = 8) where T : unmanaged, IJobParallelFor
	{
		jobQueue.Enqueue(new JobParallelForWrapper<T>(job, arrayLength, batchCount));
	}

	public void ScheduleJob(JobHandle handle)
	{
		jobHandles.Add(handle);
	}

	public async UniTask ScheduleAll()
	{
		byte count = 0;

		while (jobQueue.Count > 0)
		{
			count++;
			IJobWrapper wrapper = jobQueue.Dequeue();
			JobHandle   handle  = wrapper.Schedule();
			jobHandles.Add(handle);

			if (count < BatchSize) continue;
			await UniTask.Yield();
			count = 0;
		}

		if (count > 0) await UniTask.Yield();
	}

	public async UniTask Complete()
	{
		byte      count     = 0;
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

	public void CompleteAll()
	{
		for (var i = 0; i < jobHandles.Length; i++) jobHandles[i].Complete();
	}

	private bool AreAllJobsCompleted()
	{
		return jobHandles.AsValueEnumerable().All(job => job.IsCompleted);
	}

	public void Dispose()
	{
		CompleteAll();
		jobHandles.Dispose();
		jobQueue.Dispose();
	}
}