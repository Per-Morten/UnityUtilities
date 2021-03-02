/*
 *   MIT License
 *
 *   Copyright(c) 2019 Timothy Raines
 *
 *   Permission is hereby granted, free of charge, to any person obtaining a copy
 *   of this software and associated documentation files (the "Software"), to deal
 *   in the Software without restriction, including without limitation the rights
 *   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *   copies of the Software, and to permit persons to whom the Software is
 *   furnished to do so, subject to the following conditions:
 *
 *   The above copyright notice and this permission notice shall be included in all
 *   copies or substantial portions of the Software.
 *
 *   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *   SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

/// <summary>
/// Provides access to the internal UnityEngine.NoAllocHelpers methods.
/// </summary>
public static class NoAllocHelpers
{
    private static readonly Dictionary<Type, Delegate> ExtractArrayFromListTDelegates = new Dictionary<Type, Delegate>();
    private static readonly Dictionary<Type, Delegate> ResizeListDelegates = new Dictionary<Type, Delegate>();

    /// <summary>
    /// Extract the internal array from a list.
    /// </summary>
    /// <typeparam name="T"><see cref="List{T}"/>.</typeparam>
    /// <param name="list">The <see cref="List{T}"/> to extract from.</param>
    /// <returns>The internal array of the list.</returns>
    public static T[] ExtractArrayFromListT<T>(List<T> list)
    {
        if (!ExtractArrayFromListTDelegates.TryGetValue(typeof(T), out var obj))
        {
            var ass = Assembly.GetAssembly(typeof(Mesh)); // any class in UnityEngine
            var type = ass.GetType("UnityEngine.NoAllocHelpers");
            var methodInfo = type.GetMethod("ExtractArrayFromListT", BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(T));

            obj = ExtractArrayFromListTDelegates[typeof(T)] = Delegate.CreateDelegate(typeof(Func<List<T>, T[]>), methodInfo);
        }

        var func = (Func<List<T>, T[]>)obj;
        return func.Invoke(list);
    }

    /// <summary>
    /// Resize a list.
    /// </summary>
    /// <typeparam name="T"><see cref="List{T}"/>.</typeparam>
    /// <param name="list">The <see cref="List{T}"/> to resize.</param>
    /// <param name="size">The new length of the <see cref="List{T}"/>.</param>
    public static void ResizeList<T>(List<T> list, int size)
    {
        if (!ResizeListDelegates.TryGetValue(typeof(T), out var obj))
        {
            var ass = Assembly.GetAssembly(typeof(Mesh)); // any class in UnityEngine
            var type = ass.GetType("UnityEngine.NoAllocHelpers");
            var methodInfo = type.GetMethod("ResizeList", BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(T));
            obj = ResizeListDelegates[typeof(T)] =
                Delegate.CreateDelegate(typeof(Action<List<T>, int>), methodInfo);
        }

        var action = (Action<List<T>, int>)obj;
        action.Invoke(list, size);
    }
}
