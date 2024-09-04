// This is an independent project of an individual developer. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: https://pvs-studio.com

/*
MIT License

Copyright (c) 2023 Yury Malich http://www.malich.ru
https://github.com/ymalich

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace ObjectPools;
public sealed class StringBuilderPool
{
    public static int MaxCount { get; set; } = Math.Max(32, Environment.ProcessorCount * 2);

    public static StringBuilderPool Shared { get; private set; } = new StringBuilderPool();

    private int _countInContainer;

    private ConcurrentBag<StringBuilder> _stringBuilders = new ();

    [ThreadStatic]
    private static StringBuilder?[]? _slot;

    private static StringBuilder?[] CacheSlot
    {
        get
        {
            var slot = _slot;
            if (slot is null)
            {
                _slot = slot = new StringBuilder[1];
            }

            return slot;
        }
    }

    public StringBuilder Rent()
    {
        var slot = CacheSlot;
        var stringBuilder = slot[0];
        if (stringBuilder is not null)
        {
            slot[0] = null;
            return stringBuilder;
        }

        if (_countInContainer > 0 && _stringBuilders.TryTake(out stringBuilder))
        {
            Interlocked.Decrement(ref _countInContainer);
            return stringBuilder;
        }

        return new StringBuilder();
    }

    public void Return(StringBuilder? sb)
    {
        if (sb is null || _countInContainer >= MaxCount)
        {
            return;
        }

        sb.Clear();

        var box = CacheSlot;
        if (box[0] is null)
        {
            box[0] = sb;
            return;
        }

        if (_countInContainer < MaxCount)
        {
            Interlocked.Increment(ref _countInContainer);
            _stringBuilders.Add(sb);
        }
    }

    public void Clear()
    {
        _stringBuilders = new ();
        CacheSlot[0] = null;
    }
}