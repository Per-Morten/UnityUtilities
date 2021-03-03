///
/// MIT License
/// 
/// Copyright (c) 2021 Per-Morten Straume
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.
/// 

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public static class ViewAsNativeArrayExtensions
{
    /// <summary>
    /// View <paramref name="array"/> as a <see cref="NativeArray{T}"/> without having to copy it or doing all the boilerplate for getting a pointer to the array. 
    /// Useful for allowing a job to work on an array.
    ///
    /// <para>
    /// You do not need to dispose the <paramref name="nativeArray"/>, but you need to dispose the <see cref="NativeArrayViewHandle"/> you get back, Unity's Memory Leak Detection will tell you if you forget.
    /// Do not use the <paramref name="nativeArray"/> after calling <see cref="NativeArrayViewHandle.Dispose"/> on the <see cref="NativeArrayViewHandle"/> returned from this function, 
    /// as you can risk the garbage collector removing the data from down under you, Unity's Collections Safety Checks will tell you if you do this.
    /// There is <b>no</b> race detection for accessing multiple different views of the same array in different jobs concurrently.
    /// </para>
    /// 
    /// Usage:
    /// <code>
    /// int[] array;
    /// using (array.ViewAsNativeArray(out var nativeArray))
    /// {
    ///     // work on nativeArray
    /// }
    /// </code>
    /// </summary>
    public unsafe static NativeArrayViewHandle ViewAsNativeArray<T>(this T[] array, out NativeArray<T> nativeArray) where T : struct
    {
        var ptr = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out var handle);
        nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, array.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Create(out var safety, out var sentinel, 0, Allocator.None);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safety);
        return new NativeArrayViewHandle(handle, safety, sentinel);
#else
        return new NativeArrayViewHandle(handle);
#endif
    }

    /// <summary>
    /// View <paramref name="list"/> as a <see cref="NativeArray{T}"/> without having to copy it or doing all the boilerplate for getting the pointer out of a list. 
    /// Useful for allowing a job to work on a list.
    /// 
    /// <para>
    /// Put this thing in a disposable scope unless you can guarantee that the list will never change size or reallocate (in that case consider using a <see cref="NativeArray{T}"/> instead),
    /// as Unity will <b>not</b> tell you if you're out of bounds, accessing invalid data, or accessing stale data because you have a stale/invalid view of the list.
    /// The following changes to the list will turn a view invalid/stale:
    /// <list type="number">
    /// <item>The contents of the array will be stale (not reflect any changes to the values in the list) in case of a reallocation (changes to, or adding more items than, <see cref="List{T}.Capacity"/> or using <see cref="List{T}.TrimExcess"/>)</item>
    /// <item>The length of the array will be wrong if you add/remove elements from the list</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The <paramref name="nativeArray"/> itself does not need to be disposed, but you need to dispose the <see cref="NativeArrayViewHandle"/> you get back, Unity's Memory Leak Detection will tell you if you forget.
    /// Do not use the array after calling <see cref="NativeArrayViewHandle.Dispose"/> on the <see cref="NativeArrayViewHandle"/> returned from this function, 
    /// as you can risk the garbage collector removing the data from down under you, Unity's Collections Safety Checks will tell you if you do this.
    /// There is <b>no</b> race detection for accessing multiple different views of the same list in different jobs concurrently, or modifying the list while a job is working on a view.
    /// </para>
    /// 
    /// Usage:
    /// <code>
    /// List&lt;int&gt; list;
    /// using (list.ViewAsNativeArray(out var nativeArray))
    /// {
    ///     // work on nativeArray
    /// }
    /// </code>
    /// </summary>
    public unsafe static NativeArrayViewHandle ViewAsNativeArray<T>(this List<T> list, out NativeArray<T> nativeArray) where T : struct
    {
        var lArray = NoAllocHelpers.ExtractArrayFromListT(list);
        var ptr = UnsafeUtility.PinGCArrayAndGetDataAddress(lArray, out var handle);
        nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, list.Count, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        DisposeSentinel.Create(out var safety, out var sentinel, 0, Allocator.None);
        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safety);
        return new NativeArrayViewHandle(handle, safety, sentinel);
#else
        return new NativeArrayViewHandle(handle);
#endif
    }

    public struct NativeArrayViewHandle
        : IDisposable
    {
        private ulong m_GCHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private DisposeSentinel m_DisposeSentinel;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public NativeArrayViewHandle(ulong gcHandle, AtomicSafetyHandle safety, DisposeSentinel disposeSentinel)
        {
            m_Safety = safety;
            m_DisposeSentinel = disposeSentinel;
            m_GCHandle = gcHandle;
        }
#else
        public NativeArrayViewHandle(ulong gcHandle)
        {
            m_GCHandle = gcHandle;
        }
#endif

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            UnsafeUtility.ReleaseGCObject(m_GCHandle);
        }

        public JobHandle Dispose(JobHandle dependsOn)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Clear(ref m_DisposeSentinel);

            var jobHandle = new NativeArrayViewHandleDisposeJob
            {
                Data = new NativeArrayViewHandleDispose(m_GCHandle, m_Safety),
            }
            .Schedule(dependsOn);

            AtomicSafetyHandle.Release(m_Safety);
            return jobHandle;
#else
            return new NativeArrayViewHandleDisposeJob
            {
                Data = new NativeArrayViewHandleDispose(m_GCHandle),
            }
            .Schedule(dependsOn);
#endif
        }

        [NativeContainer]
        private struct NativeArrayViewHandleDispose
        {
            private ulong m_GCHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public NativeArrayViewHandleDispose(ulong gcHandle, AtomicSafetyHandle safety)
            {
                m_GCHandle = gcHandle;
                m_Safety = safety;
            }
#else
            public NativeArrayViewHandleDispose(ulong gcHandle)
            {
                m_GCHandle = gcHandle;
            }
#endif

            public void Dispose()
            {
                UnsafeUtility.ReleaseGCObject(m_GCHandle);
            }
        }

        [BurstCompile]
        private struct NativeArrayViewHandleDisposeJob : IJob
        {
            public NativeArrayViewHandleDispose Data;

            public void Execute()
            {
                Data.Dispose();
            }
        }
    }
}
