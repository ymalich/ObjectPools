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
using System.Collections.Generic;
using System.Threading;

namespace ObjectPools;

public sealed class ListPool<T>
{
    public static int MaxCount { get; set; } = Math.Max(32, Environment.ProcessorCount * 2);

    public static ListPool<T> Shared { get; } = new ();

    private ConcurrentBag<List<T>> _listContainer = new ();
    private int _countInContainer = 0;

    [ThreadStatic]
    private static List<T>?[]? _slot;

    private static List<T>?[] CacheSlot
    {
        get
        {
            var slot = _slot;
            if (slot is null) { _slot = slot = new List<T>[1]; }

            return slot;
        }
    }

    public List<T> Rent()
    {
        var slot = CacheSlot;
        var list = slot[0];
        if (list is not null)
        {
            slot[0] = null;
            return list;
        }

        if (_countInContainer > 0 && _listContainer.TryTake(out list))
        {
            Interlocked.Decrement(ref _countInContainer);
            return list;
        }

        return new List<T>();
    }

    public void Return(ref List<T>? listRef)
    {
        var list = listRef;
        listRef = null;
        if (list is null || _countInContainer >= MaxCount)
        {
            return;
        }

        list.Clear();

        var box = CacheSlot;
        if (box[0] is null)
        {
            box[0] = list;
            return;
        }

        if (_countInContainer < MaxCount)
        {
            _listContainer.Add(list);
            Interlocked.Increment(ref _countInContainer);
        }
    }

    public void Clear()
    {
        _listContainer = new ();
        CacheSlot[0] = null;
    }

    public List<T> RentAndAdd(T item)
    {
        var list = Rent();
        list.Add(item);
        return list;
    }

    public List<T> RentAndAddRange(IEnumerable<T> items)
    {
        var list = Rent();
        list.AddRange(items);
        return list;
    }

    public RentedListWrapper<T> RentWrapped() => new (Rent());

    public RentedListWrapper<T> RentWrappedAndAdd(T item) => new (RentAndAdd(item));

    public RentedListWrapper<T> RentWrappedAndAddRange(IEnumerable<T> item) => new (RentAndAddRange(item));
}
