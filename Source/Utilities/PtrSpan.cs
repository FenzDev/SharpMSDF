using System;
using Typography.OpenFont.CFF;

namespace SharpMSDF.Utilities
{
    public readonly unsafe struct PtrSpan<T> where T : unmanaged
    {
        public readonly T* Data;
        public readonly int Capacity; // Total allocated slots
        public readonly int Count;    // Active element count (like Length)

        public PtrSpan(T* dataPtr, int capacity, int count)
        {
#if DEBUG
            if ((uint)count > (uint)capacity) throw new IndexOutOfRangeException();
#endif
            Data = dataPtr;
            Capacity = capacity;
            Count = count;
        }

        public ref T this[int index]
        {
            get
            {
#if DEBUG
                if ((uint)index >= (uint)Count) throw new IndexOutOfRangeException();
#endif
                return ref Data[index];
            }
        }

        public static void Push(ref PtrSpan<T> span, T value)
        {
            span = new PtrSpan<T>(span.Data, span.Capacity, span.Count + 1);
            span[span.Count-1] = value;
        }
        public static T Pop(ref PtrSpan<T> span)
        {
            T elm = span[span.Count - 1];
            span = new PtrSpan<T>(span.Data, span.Capacity, span.Count - 1);
            return elm;
        }
        public static void Clear(ref PtrSpan<T> span)
        {
            span = new PtrSpan<T>(span.Data, span.Capacity, 0);
        }

        public static T* operator +(PtrSpan<T> span, int index)
        {
#if DEBUG
            if ((uint)index >= (uint)span.Count) throw new IndexOutOfRangeException();
#endif
            return span.Data + index;
        }

        public readonly Span<T> AsSpan() => new Span<T>(Data, Count);
    }
}
