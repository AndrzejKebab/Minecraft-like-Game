using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using static Unity.Collections.LowLevel.Unsafe.UnsafeUtility;

namespace _Project.WorldGeneration
{
	public static class NativeCurveExtensions
	{
		public static NativeCurve ToNative(this AnimationCurve curve, Allocator allocator = Allocator.TempJob)
		{
			return new NativeCurve(curve, allocator);
		}
	}

	[NativeContainer]
	[NativeContainerIsReadOnly]
	[NativeContainerSupportsDeallocateOnJobCompletion]
	[StructLayout(LayoutKind.Sequential)]
	public struct NativeCurve : IDisposable
	{
		#region API

		public bool IsCreated => m_Buffer != IntPtr.Zero;
		public int  Count     { get; private set; }

		public float StartTime
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
				return _startTime;
			}
		}

		public float EndTime
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
				return _endTime;
			}
		}

		public float Duration
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
				return _endTime - _startTime;
			}
		}

		public KeyFrame this[int index]
		{
			get
			{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
				if (index < 0 || index >= Count) throw new IndexOutOfRangeException();
#endif
				unsafe
				{
					return ReadArrayElement<KeyFrame>((void*)m_Buffer, index);
				}
			}
			private set
			{
				unsafe
				{
					WriteArrayElement((void*)m_Buffer, index, value);
				}
			}
		}

		public NativeCurve(AnimationCurve curve, Allocator allocator = Allocator.Persistent)
			: this(curve.length, allocator, curve.preWrapMode, curve.postWrapMode)
		{
			for (var i = 0; i < Count; i++) this[i] = curve[i];
			Init();
		}

		public NativeCurve(IList<Keyframe> keyframes,                    Allocator allocator    = Allocator.Persistent,
		                   WrapMode        preWrapMode = WrapMode.Clamp, WrapMode  postWrapMode = WrapMode.Clamp)
			: this(keyframes.Count, allocator, preWrapMode, postWrapMode)
		{
			for (var i = 0; i < Count; i++) this[i] = keyframes[i];
			Init();
		}

		public NativeCurve(IList<KeyFrame> keyframes,                    Allocator allocator    = Allocator.Persistent,
		                   WrapMode        preWrapMode = WrapMode.Clamp, WrapMode  postWrapMode = WrapMode.Clamp)
			: this(keyframes.Count, allocator, preWrapMode, postWrapMode)
		{
			for (var i = 0; i < Count; i++) this[i] = keyframes[i];
			Init();
		}

		public NativeCurve(NativeSlice<KeyFrame> keyframes,                    Allocator allocator = Allocator.Persistent,
		                   WrapMode              preWrapMode = WrapMode.Clamp, WrapMode  postWrapMode = WrapMode.Clamp)
			: this(keyframes.Length, allocator, preWrapMode, postWrapMode)
		{
			for (var i = 0; i < Count; i++) this[i] = keyframes[i];
			Init();
		}

		public void Dispose()
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			// Fixed: use unmanaged Release
			AtomicSafetyHandle.Release(m_Safety);
#endif
			Count = 0;

			if (m_Buffer != IntPtr.Zero)
				unsafe
				{
					Free((void*)m_Buffer, m_AllocatorLabel);
					m_Buffer = IntPtr.Zero;
				}
		}

		public float Evaluate(float t)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
			if (Count == 1) return this[0].Value;

			if (t < _startTime)
				AdjustTimeWithMode(ref t, _preWrapMode);
			else if (t >= _endTime) AdjustTimeWithMode(ref t, _postWrapMode);

			var lowerBound = 0;
			var upperBound = Count - 1;

			while (true)
			{
				if (upperBound == lowerBound + 1) break;

				var midpoint = lowerBound + (upperBound - lowerBound) / 2;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
				Assert.AreNotEqual(lowerBound, midpoint);
				Assert.AreNotEqual(upperBound, midpoint);
#endif

				if (this[midpoint].Time > t)
					upperBound = midpoint;
				else
					lowerBound = midpoint;
			}

			return EvalCurved(this[lowerBound], this[upperBound], t);
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct KeyFrame
		{
			public float Time;
			public float Value;
			public float InTangent, OutTangent;

			public static implicit operator KeyFrame(Keyframe keyframe)
			{
				return new KeyFrame
				       {
					       Time       = keyframe.time,
					       Value      = keyframe.value,
					       InTangent  = keyframe.inTangent,
					       OutTangent = keyframe.outTangent
				       };
			}
		}

		#endregion

		#region IMPLEMENTATION

		private float _startTime;
		private float _endTime;

		private                                     WrapMode  _preWrapMode, _postWrapMode;
		[NativeDisableUnsafePtrRestriction] private IntPtr    m_Buffer;
		private                                     Allocator m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		// Fixed: Strip DisposeSentinel as it's a managed class and breaks ECS
		internal AtomicSafetyHandle m_Safety;
#endif

		private NativeCurve(int keyframes, Allocator allocator, WrapMode preWrapMode, WrapMode postWrapMode)
		{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (keyframes <= 0) throw new ArgumentException("The number of keyframes must be greater than zero.");

			switch (allocator)
			{
				case Allocator.Persistent:
				case Allocator.Temp:
				case Allocator.TempJob:
					break;
				default:
					throw new
						ArgumentException($"Expected an allocator type of Persistent, Temp, or TempJob, but got {allocator}.");
			}

			// Fixed: use unmanaged AtomicSafetyHandle.Create
			m_Safety = AtomicSafetyHandle.Create();
#endif

			Count         = keyframes;
			_preWrapMode  = preWrapMode;
			_postWrapMode = postWrapMode;

			_startTime = 0;
			_endTime   = 0;
			unsafe
			{
				m_Buffer         = (IntPtr)Malloc(SizeOf<KeyFrame>() * Count, AlignOf<KeyFrame>(), allocator);
				m_AllocatorLabel = allocator;
			}
		}

		private void Init()
		{
			_startTime = this[0].Time;
			_endTime   = this[Count - 1].Time;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			for (var i = 0; i < Count; i++)
			{
				KeyFrame kf = this[i];
				if (float.IsNaN(kf.Value) || float.IsInfinity(kf.Value))
					throw
						new ArgumentException($"KeyFrame {i} had a value of {kf.Value}, which was expected to be finite.");
				if (float.IsNaN(kf.Time) || float.IsInfinity(kf.Time))
					throw new ArgumentException($"KeyFrame {i} had a time of {kf.Time}, which was expected to be finite.");
				if (float.IsNaN(kf.InTangent) || float.IsNaN(kf.OutTangent))
					throw new ArgumentException($"KeyFrame {i} had a tangent that was NaN.");
			}

			if (Count >= 2)
			{
				if (this[0].Time == this[1].Time)
					throw new ArgumentException("The first two keyframes of a NativeCurve cannot have the same time.");

				if (this[Count - 1].Time == this[Count - 2].Time)
					throw new ArgumentException("The last two keyframes of a NativeCurve cannot have the same time.");
			}

			for (var i = 1; i < Count; i++)
			{
				var t0 = this[i - 1].Time;
				var t1 = this[i].Time;
				if (t0 > t1)
					throw new
						ArgumentException($"Keyframe {i - 1} at time {t0} should come after Keyframe {i} at time {t1}.");
			}
#endif
		}

		private void AdjustTimeWithMode(ref float t, WrapMode mode)
		{
			switch (mode)
			{
				case WrapMode.Loop:
					t = _startTime + Mathf.Repeat(t - _startTime, _endTime - _startTime);
					break;
				case WrapMode.PingPong:
					t = _startTime + Mathf.PingPong(t - _startTime, _endTime - _startTime);
					break;
				default:
					t = Mathf.Clamp(t, _startTime, _endTime);
					break;
			}
		}

		private float EvalCurved(KeyFrame keyframe0, KeyFrame keyframe1, float time)
		{
			var dt = keyframe1.Time - keyframe0.Time;
			var t  = (time - keyframe0.Time) / dt;

			var m0 = keyframe0.OutTangent * dt;
			var m1 = keyframe1.InTangent * dt;

			if (float.IsInfinity(m0) || float.IsInfinity(m1)) return keyframe0.Value;

			var t2 = t * t;
			var t3 = t2 * t;

			var a = 2 * t3 - 3 * t2 + 1;
			var b = t3 - 2 * t2 + t;
			var c = t3 - t2;
			var d = -2 * t3 + 3 * t2;

			return a * keyframe0.Value + b * m0 + c * m1 + d * keyframe1.Value;
		}

		#endregion
	}
}