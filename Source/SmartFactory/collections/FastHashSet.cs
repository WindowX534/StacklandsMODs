﻿// MIT License
//
// Copyright (c) 2024. SuperComic (ekfvoddl3535@naver.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using SuperComicLib.Runtime;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SuperComicLib.Stacklands.Collections
{
    public struct FastHashSet<T>
        where T : IHashedEquatable<T>
    {
        private const string NO_PARALLEL_ERR = "ConcurrencyNotSupported";
        
        private const int StartOfFreeList = -3;

        internal readonly int[] _buckets;
        internal readonly Entry[] _entries;

        private int _count;
        private int _freeList;
        private int _freeCount;

        private readonly int _hashMask;

        public FastHashSet(int powOf2Count)
        {
            if ((powOf2Count & (powOf2Count - 1)) != 0)
                throw new ArgumentException("not power of 2 number. allow: (4, 8, 16, 32...)", nameof(powOf2Count));

            powOf2Count = Mathf.Max(powOf2Count, 4);

            _buckets = new int[powOf2Count];
            _entries = new Entry[powOf2Count];

            _hashMask = powOf2Count - 1;

            _count = 0;
            _freeList = -1;
            _freeCount = 0;
        }

        public readonly int Count => _count;

        public bool Add(T item)
        {
            int hashCode = item.GetHashCode();

            uint collisionCount = 0;

            ref int bucket = ref _buckets[hashCode & _hashMask];
            for (int i = bucket - 1; i >= 0;)
            {
                ref var entry = ref _entries[i];
                if (entry.hashcode == hashCode && entry.value.Equals(item))
                    return false;

                i = entry.next;

                if (collisionCount++ > (uint)_hashMask)
                    throw new InvalidOperationException(FastHashSet<T>.NO_PARALLEL_ERR);
            }

            int index;
            if (_freeCount > 0)
            {
                _freeCount--;

                index = _freeList;
                _freeList = StartOfFreeList - _entries[_freeList].next;
            }
            else
            {
                int count = _count;
                if (count >= _hashMask + 1)
                    return false;

                index = count;
                _count = count + 1;
            }

            ref var e = ref _entries[index];
            e.value = item;
            e.hashcode = hashCode;
            e.next = bucket - 1;

            bucket = index + 1;

            return true;
        }

        public bool Remove(T item)
        {
            var entries = _entries;

            uint collisionCount = 0;
            int lastIndex = -1;

            int hashCode = item.GetHashCode();

            ref int bucket = ref _buckets[hashCode & _hashMask];
            for (int i = bucket - 1; i >= 0;)
            {
                ref var entry = ref _entries[i];

                if (entry.hashcode == hashCode && entry.value.Equals(item))
                {
                    if (lastIndex < 0)
                        bucket = entry.next + 1;
                    else
                        entries[lastIndex].next = entry.next;

                    entry.next = StartOfFreeList - _freeList;

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        entry.value = default;

                    _freeList = i;
                    _freeCount++;

                    return true;
                }

                lastIndex = i;
                i = entry.next;

                if (collisionCount++ > (uint)_hashMask)
                    throw new InvalidOperationException(FastHashSet<T>.NO_PARALLEL_ERR);
            }

            return false;
        }

        public void Clear()
        {
            int count = _count;
            if (count <= 0) 
                return;

            FastClear();

            _entries.AsSpan(0, count).Clear();
        }

        public void FastClear()
        {
            _count = 0;
            _freeList = -1;
            _freeCount = 0;

            _buckets.AsSpan().Clear();
        }

        public readonly void ForceSetCapacity(int capacity)
        {
            XUnsafe.AsRef(in _buckets) = new int[capacity];
            XUnsafe.AsRef(in _entries) = new Entry[capacity];

            XUnsafe.AsRef(in _hashMask) = capacity - 1;
        }

        internal struct Entry
        {
            public T value;
            public int hashcode;
            public int next;
        }
    }
}
