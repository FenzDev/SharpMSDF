using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Utilities
{
    public unsafe struct PtrPool<T>(T* data, int capacity) where T : unmanaged
    {
        private readonly T* _Data = data;
        private readonly int _Capacity = capacity;
        private int _Reserved;

        public PtrSpan<T> Reserve(int amount)
        {
#if DEBUG
            if ((uint)(_Reserved + amount) > (uint)_Capacity || amount <= 0)
                throw new ArgumentOutOfRangeException("amount");
#endif
            return new PtrSpan<T>(_Data+_Reserved++, amount, 0);
        }
        public T* ReserveOne()
        {
#if DEBUG
            if ((uint)_Reserved >= (uint)_Capacity)
                throw new OverflowException();
#endif
            return _Data+_Reserved++;
        }

    }
}
