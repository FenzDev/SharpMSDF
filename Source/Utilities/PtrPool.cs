using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Utilities
{
    public unsafe struct PtrPool<T>(T* data, int capacity) where T : unmanaged
    {
        private int _Reserved;
        public readonly T* Data = data;
        public readonly int Capacity = capacity;

        public PtrSpan<T> Reserve(int amount)
        {
#if DEBUG
            if ((uint)(_Reserved + amount) > (uint)Capacity || amount <= 0)
                throw new ArgumentOutOfRangeException("amount");
#endif
            var reserveIdx = _Reserved; _Reserved += amount;
            return new PtrSpan<T>(Data+reserveIdx, amount, 0);
        }
        public T* ReserveOne()
        {
#if DEBUG
            if ((uint)_Reserved >= (uint)Capacity)
                throw new OverflowException();
#endif
            return Data+_Reserved++;
        }

        public override string ToString() => $"PtrPool<{typeof(T).Name}> Capacity={Capacity}";
    }
}
