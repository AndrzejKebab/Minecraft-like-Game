using ZLinq;

[assembly: ZLinqDropIn("", DropInGenerateTypes.Everything)]
[assembly: ZLinqDropInExternalExtension("", "System.Collections.Generic.IReadOnlyCollection`1")]
[assembly: ZLinqDropInExternalExtension("", "System.Collections.Generic.IReadOnlyList`1")]
[assembly: ZLinqDropInExternalExtension("", "Unity.Collections.NativeArray`1", "ZLinq.Linq.FromNativeArray`1")]
[assembly: ZLinqDropInExternalExtension("", "Unity.Collections.NativeArray`1+ReadOnly", "ZLinq.Linq.FromNativeArray`1")]
[assembly: ZLinqDropInExternalExtension("", "Unity.Collections.NativeSlice`1", "ZLinq.Linq.FromNativeSlice`1")]
[assembly: ZLinqDropInExternalExtension("", "Unity.Collections.NativeList`1", "ZLinq.Linq.FromNativeList`1")]