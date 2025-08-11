using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{

    /// Interface for a “size selector” that iterates candidate atlas sizes.
    public interface ISizeSelector
    {
        /// Prepare the selector with a known total area.
        void Initialize(int totalArea);

        /// Advance to the next (width, height). Returns false when exhausted.
        bool Next(out int width, out int height);

        /// Step back one iteration (to pick the last good size).
        void Decrement();

        /// Step forward one iteration (to skip an unsuccessful size).
        void Increment();
    }

    /// <summary>
    /// Selects square dimensions which are also a multiple of MULTIPLE.
    /// </summary>
    public struct SquareSizeSelector : ISizeSelector
    {
        private readonly int _multiple;
        private int _lowerBound, _upperBound, _current;

        public SquareSizeSelector() => _multiple = 1;
        public SquareSizeSelector(int multiple = 1) => _multiple = multiple;

        public void Initialize(int totalArea)
        {
            _upperBound = -1;
            if (totalArea > 0)
                _lowerBound = (int)Math.Sqrt(totalArea - 1) / _multiple + 1;
            else
                _lowerBound = 0;
            UpdateCurrent();
        }

        public bool Next(out int width, out int height)
        {
            width = _multiple * _current;
            height = _multiple * _current;
            return (_lowerBound < _upperBound) || (_upperBound < 0);
        }

        public void Increment()
        {
            // ++ : raise lower bound
            _lowerBound = _current + 1;
            UpdateCurrent();
        }

        public void Decrement()
        {
            // -- : lower the upper bound
            _upperBound = _current;
            UpdateCurrent();
        }

        private void UpdateCurrent()
        {
            if (_upperBound < 0)
                // heuristic start
                _current = 5 * _lowerBound / 4 + 16 / _multiple + 1;
            else
                _current = _lowerBound + (_upperBound - _lowerBound) / 2;
        }
    }

    /// <summary>
    /// Selects square power‐of‐two dimensions.
    /// </summary>
    public struct SquarePowerOfTwoSizeSelector : ISizeSelector
    {
        private int _side;

        public void Initialize(int totalArea)
        {
            _side = 1;
            while (_side * _side < totalArea)
                _side <<= 1;
        }

        public bool Next(out int width, out int height)
        {
            width = _side;
            height = _side;
            return _side > 0;
        }

        public void Increment()
        {
            _side <<= 1;
        }

        public void Decrement()
        {
            // signal “give up” by zeroing
            _side = 0;
        }
    }

    /// <summary>
    /// Selects square or 2:1‐ratio power‐of‐two dimensions.
    /// </summary>
    public struct PowerOfTwoSizeSelector : ISizeSelector
    {
        private int _w, _h;

        public void Initialize(int totalArea)
        {
            _w = _h = 1;
            // mimic ctor loop: ++*this until w*h >= totalArea
            while (_w * _h < totalArea)
                Increment();
        }

        public bool Next(out int width, out int height)
        {
            width = _w;
            height = _h;
            return _w > 0 && _h > 0;
        }

        public void Increment()
        {
            if (_w == _h)
                _w <<= 1;
            else
                _h = _w;
        }

        public void Decrement()
        {
            // signal “done” by zeroing
            _w = _h = 0;
        }
    }
}
