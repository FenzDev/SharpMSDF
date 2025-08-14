
using System.Numerics;
using static SharpMSDF.Core.TrueDistanceSelector;

namespace SharpMSDF.Core
{
    public interface IDistanceSelector<TSelf, TDistance> where TSelf : IDistanceSelector<TSelf, TDistance>
    {
        /// <summary>
        /// Reset any internal state for a new query point p.
        /// </summary>
        unsafe void AddEdge(Span<EdgeCache> cache, int cacheIndex, EdgeSegment prevEdge, EdgeSegment edge, EdgeSegment nextEdge);

        /// <summary>
        /// Reset any internal state for a new query point p.
        /// </summary>
        void Reset(Vector2 p);

        /// <summary>
        /// Merge with another selector (for parallel reduction).
        /// </summary>
        void Merge(TSelf other);

        /// <summary>
        /// Return the final distance value of type TDistance.
        /// </summary>
        TDistance Distance();
    }

    public struct MultiDistance
    {
        public float R, G, B;
    }

    public struct MultiAndTrueDistance
    {
        public float R, G, B, A;
    }

    public struct EdgeCache
    {
        public Vector2 Point { get; set; }
        public float AbsDistance { get; set; }
        public float ADomainDistance, BDomainDistance;
        public float APerpDistance, BPerpDistance;
        public EdgeCache()
        {
            Point = default;
            AbsDistance = 0;
            ADomainDistance = BDomainDistance = 0;
            APerpDistance = BPerpDistance = 0;
        }
    }

    public struct TrueDistanceSelector : IDistanceSelector<TrueDistanceSelector, float>
    {
        private Vector2 _p;
        private SignedDistance _minDistance = new SignedDistance();

        private const float DISTANCE_DELTA_FACTOR = 1.001f;

        public TrueDistanceSelector()
        {
        }

        public void Reset(Vector2 p)
        {
            float delta = DISTANCE_DELTA_FACTOR * (p - _p).Length();
            _minDistance.Distance += Arithmetic.NonZeroSign(_minDistance.Distance) * delta;
            _p = p;
        }

        public unsafe void AddEdge(Span<EdgeCache> cache, int index, EdgeSegment prevEdge, EdgeSegment edge, EdgeSegment nextEdge)
        {
            var edgeCache = cache[index];
            float delta = DISTANCE_DELTA_FACTOR * (_p - edgeCache.Point).Length();
            if (edgeCache.AbsDistance - delta <= MathF.Abs(_minDistance.Distance))
            {
                var distance = edge.SignedDistance(_p, out _);
                if (distance < _minDistance)
                    _minDistance = distance;
                edgeCache.Point = _p;
                edgeCache.AbsDistance = MathF.Abs(distance.Distance);
            }
            cache[index] = edgeCache;
        }

        public void Merge(TrueDistanceSelector other)
        {
            if (other._minDistance < _minDistance)
                _minDistance = other._minDistance;
        }

        

        public float Distance() => _minDistance.Distance;

    }

    public struct PerpendicularDistanceSelectorBase
    {

        internal SignedDistance _minTrueDistance = new();
        internal float _minNegPerp, _minPosPerp;
        internal EdgeSegment _nearEdge;
        internal float _nearEdgeParam;

        public const float DISTANCE_DELTA_FACTOR = 1.001f;

        public PerpendicularDistanceSelectorBase()
        {
            _minNegPerp = -MathF.Abs(_minTrueDistance.Distance);
            _minPosPerp = MathF.Abs(_minTrueDistance.Distance);
            _nearEdge = default;
            _nearEdgeParam = 0;
        }

        public static bool GetPerpendicularDistance(ref float distance, Vector2 ep, Vector2 edgeDir)
        {
            float ts = Vector2.Dot(ep, edgeDir);
            if (ts > 0)
            {
                float perp = ep.Cross(edgeDir);
                if (MathF.Abs(perp) < MathF.Abs(distance))
                {
                    distance = perp;
                    return true;
                }
            }
            return false;
        }

        public void Reset(float delta)
        {
            _minTrueDistance.Distance += Arithmetic.NonZeroSign(_minTrueDistance.Distance) * delta;
            _minNegPerp = -MathF.Abs(_minTrueDistance.Distance);
            _minPosPerp = MathF.Abs(_minTrueDistance.Distance);
            _nearEdge = default;
            _nearEdgeParam = 0;
        }

        public bool IsEdgeRelevant(EdgeCache cache, EdgeSegment edge, Vector2 p)
        {
            float delta = DISTANCE_DELTA_FACTOR * (p - cache.Point).Length();
            return
                cache.AbsDistance - delta <= MathF.Abs(_minTrueDistance.Distance)
                || MathF.Abs(cache.ADomainDistance) < delta
                || MathF.Abs(cache.BDomainDistance) < delta
                || (cache.ADomainDistance > 0 && (
                        cache.APerpDistance < 0
                            ? cache.APerpDistance + delta >= _minNegPerp
                            : cache.APerpDistance - delta <= _minPosPerp
                   ))
                || (cache.BDomainDistance > 0 && (
                        cache.BPerpDistance < 0
                            ? cache.BPerpDistance + delta >= _minNegPerp
                            : cache.BPerpDistance - delta <= _minPosPerp
                   ));
        }

        internal void AddEdgeTrueDistance(EdgeSegment edge, SignedDistance dist, float param)
        {
            if (dist < _minTrueDistance)
            {
                _minTrueDistance = dist;
                _nearEdge = edge;
                _nearEdgeParam = param;
            }
        }

        internal void AddEdgePerpendicularDistance(float d)
        {
            if (d <= 0 && d > _minNegPerp) _minNegPerp = d;
            if (d >= 0 && d < _minPosPerp) _minPosPerp = d;
        }

        public void Merge(PerpendicularDistanceSelectorBase other)
        {
            if (other._minTrueDistance < _minTrueDistance)
            {
                _minTrueDistance = other._minTrueDistance;
                _nearEdge = other._nearEdge;
                _nearEdgeParam = other._nearEdgeParam;
            }
            if (other._minNegPerp > _minNegPerp) _minNegPerp = other._minNegPerp;
            if (other._minPosPerp < _minPosPerp) _minPosPerp = other._minPosPerp;
        }

        internal float ComputeDistance(Vector2 p)
        {
            float best = _minTrueDistance.Distance < 0 ? _minNegPerp : _minPosPerp;
            if (_nearEdge.IsAssigned)
            {
                var sd = _minTrueDistance;
                _nearEdge.DistanceToPerpendicularDistance(ref sd, p, _nearEdgeParam);
                if (MathF.Abs(sd.Distance) < MathF.Abs(best))
                    best = sd.Distance;
            }
            return best;
        }

        public SignedDistance TrueDistance() => _minTrueDistance;

    }

    public struct PerpendicularDistanceSelector : IDistanceSelector<PerpendicularDistanceSelector, float>
    {
        private Vector2 _p;
        public float DistanceType;  // in C# you can drop this: use float directly

        internal SignedDistance _minTrueDistance = new SignedDistance();
        internal float _minNegPerp, _minPosPerp;
        internal EdgeSegment _nearEdge;
        internal float _nearEdgeParam;

        public const float DISTANCE_DELTA_FACTOR = 1.001f;

        public PerpendicularDistanceSelector()
        {
            _minNegPerp = -MathF.Abs(_minTrueDistance.Distance);
            _minPosPerp = MathF.Abs(_minTrueDistance.Distance);
            _nearEdge = default;
            _nearEdgeParam = 0;
        }

        public static bool GetPerpendicularDistance(ref float distance, Vector2 ep, Vector2 edgeDir)
        {
            float ts = Vector2.Dot(ep, edgeDir);
            if (ts > 0)
            {
                float perp = ep.Cross(edgeDir);
                if (MathF.Abs(perp) < MathF.Abs(distance))
                {
                    distance = perp;
                    return true;
                }
            }
            return false;
        }

        public void Reset(float delta)
        {
            _minTrueDistance.Distance += Arithmetic.NonZeroSign(_minTrueDistance.Distance) * delta;
            _minNegPerp = -MathF.Abs(_minTrueDistance.Distance);
            _minPosPerp = MathF.Abs(_minTrueDistance.Distance);
            _nearEdge = default;
            _nearEdgeParam = 0;
        }

        public bool IsEdgeRelevant(EdgeCache cache, EdgeSegment edge, Vector2 p)
        {
            float delta = DISTANCE_DELTA_FACTOR * (p - cache.Point).Length();
            return
                cache.AbsDistance - delta <= MathF.Abs(_minTrueDistance.Distance)
                || MathF.Abs(cache.ADomainDistance) < delta
                || MathF.Abs(cache.BDomainDistance) < delta
                || (cache.ADomainDistance > 0 && (
                        cache.APerpDistance < 0
                            ? cache.APerpDistance + delta >= _minNegPerp
                            : cache.APerpDistance - delta <= _minPosPerp
                   ))
                || (cache.BDomainDistance > 0 && (
                        cache.BPerpDistance < 0
                            ? cache.BPerpDistance + delta >= _minNegPerp
                            : cache.BPerpDistance - delta <= _minPosPerp
                   ));
        }

        internal void AddEdgeTrueDistance(EdgeSegment edge, SignedDistance dist, float param)
        {
            if (dist < _minTrueDistance)
            {
                _minTrueDistance = dist;
                _nearEdge = edge;
                _nearEdgeParam = param;
            }
        }

        internal void AddEdgePerpendicularDistance(float d)
        {
            if (d <= 0 && d > _minNegPerp) _minNegPerp = d;
            if (d >= 0 && d < _minPosPerp) _minPosPerp = d;
        }

        //public void Merge(PerpendicularDistanceSelectorBase other)
        //{
        //}

        internal float ComputeDistance(Vector2 p)
        {
            float best = _minTrueDistance.Distance < 0 ? _minNegPerp : _minPosPerp;
            if (_nearEdge.IsAssigned)
            {
                var sd = _minTrueDistance;
                _nearEdge.DistanceToPerpendicularDistance(ref sd, p, _nearEdgeParam);
                if (MathF.Abs(sd.Distance) < MathF.Abs(best))
                    best = sd.Distance;
            }
            return best;
        }

        public SignedDistance TrueDistance() => _minTrueDistance;

        public void Reset(Vector2 p)
        {
            float delta = DISTANCE_DELTA_FACTOR * (p - _p).Length();
            Reset(delta);
            _p = p;
        }

        public void Merge(PerpendicularDistanceSelector other)
        {
            if (other._minTrueDistance < _minTrueDistance)
            {
                _minTrueDistance = other._minTrueDistance;
                _nearEdge = other._nearEdge;
                _nearEdgeParam = other._nearEdgeParam;
            }
            if (other._minNegPerp > _minNegPerp) _minNegPerp = other._minNegPerp;
            if (other._minPosPerp < _minPosPerp) _minPosPerp = other._minPosPerp;
        }

        public void AddEdge(Span<EdgeCache> cache, int index, EdgeSegment prevEdge, EdgeSegment edge, EdgeSegment nextEdge)
        {
            var edgeCache = cache[index];
            if (IsEdgeRelevant(edgeCache, edge, _p))
            {
                float param;
                var dist = edge.SignedDistance(_p, out param);
                AddEdgeTrueDistance(edge, dist, param);
                edgeCache.Point = _p;
                edgeCache.AbsDistance = MathF.Abs(dist.Distance);

                Vector2 ap = _p - edge.Point(0);
                Vector2 bp = _p - edge.Point(1);
                Vector2 aDir = edge.Direction(0).Normalize(true);
                Vector2 bDir = edge.Direction(1).Normalize(true);
                Vector2 prevDir = prevEdge.Direction(1).Normalize(true);
                Vector2 nextDir = nextEdge.Direction(0).Normalize(true);

                float add = Vector2.Dot(ap, (prevDir + aDir).Normalize());
                float bdd = -Vector2.Dot(bp, (bDir + nextDir).Normalize());

                if (add > 0)
                {
                    float pd = dist.Distance;
                    if (GetPerpendicularDistance(ref pd, ap, -aDir))
                        AddEdgePerpendicularDistance(pd = -pd);
                    edgeCache.APerpDistance = pd;
                }
                if (bdd > 0)
                {
                    float pd = dist.Distance;
                    if (GetPerpendicularDistance(ref pd, bp, bDir))
                        AddEdgePerpendicularDistance(pd);
                    edgeCache.BPerpDistance = pd;
                }
                edgeCache.ADomainDistance = add;
                edgeCache.BDomainDistance = bdd;
            }
            cache[index] = edgeCache;
        }

        public float Distance() => ComputeDistance(_p);
    }

    public struct MultiDistanceSelector : IDistanceSelector<MultiDistanceSelector, MultiDistance>
    {
        private Vector2 _p;
        private PerpendicularDistanceSelectorBase _r = new PerpendicularDistanceSelectorBase();
        private PerpendicularDistanceSelectorBase _g = new PerpendicularDistanceSelectorBase();
        private PerpendicularDistanceSelectorBase _b = new PerpendicularDistanceSelectorBase();

        public MultiDistanceSelector()
        {
            
        }

        public void Reset(Vector2 p)
        {
            float delta = PerpendicularDistanceSelectorBase.DISTANCE_DELTA_FACTOR * (p - _p).Length();
            _r.Reset(delta);
            _g.Reset(delta);
            _b.Reset(delta);
            _p = p;
        }

        public void AddEdge(Span<EdgeCache> cache, int index, EdgeSegment prev, EdgeSegment edge, EdgeSegment next)
        {
            var edgeCache = cache[index];
            EdgeColor color = edge.Color;
            bool doR = (color & EdgeColor.Red) != 0 && _r.IsEdgeRelevant(edgeCache, edge, _p);
            bool doG = (color & EdgeColor.Green) != 0 && _g.IsEdgeRelevant(edgeCache, edge, _p);
            bool doB = (color & EdgeColor.Blue) != 0 && _b.IsEdgeRelevant(edgeCache, edge, _p);
            if (doR || doG || doB)
            {
                float param;
                var dist = edge.SignedDistance(_p, out param);
                if (doR) _r.AddEdgeTrueDistance(edge, dist, param);
                if (doG) _g.AddEdgeTrueDistance(edge, dist, param);
                if (doB) _b.AddEdgeTrueDistance(edge, dist, param);
                edgeCache.Point = _p;
                edgeCache.AbsDistance = MathF.Abs(dist.Distance);

                Vector2 ap = _p - edge.Point(0);
                Vector2 bp = _p - edge.Point(1);
                Vector2 aDir = edge.Direction(0).Normalize(true);
                Vector2 bDir = edge.Direction(1).Normalize(true);
                Vector2 prevDir = prev.Direction(1).Normalize(true);
                Vector2 nextDir = next.Direction(0).Normalize(true);
                float add = Vector2.Dot(ap, (prevDir + aDir).Normalize(true));
                float bdd = -Vector2.Dot(bp, (bDir + nextDir).Normalize(true));

                if (add > 0)
                {
                    float pd = dist.Distance;
                    if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, ap, -aDir))
                    {
                        pd = -pd;
                        if (doR) _r.AddEdgePerpendicularDistance(pd);
                        if (doG) _g.AddEdgePerpendicularDistance(pd);
                        if (doB) _b.AddEdgePerpendicularDistance(pd);
                    }
                    edgeCache.APerpDistance = pd;
                }
                if (bdd > 0)
                {
                    float pd = dist.Distance;
                    if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, bp, bDir))
                    {
                        if (doR) _r.AddEdgePerpendicularDistance(pd);
                        if (doG) _g.AddEdgePerpendicularDistance(pd);
                        if (doB) _b.AddEdgePerpendicularDistance(pd);
                    }
                    edgeCache.BPerpDistance = pd;
                }
                edgeCache.ADomainDistance = add;
                edgeCache.BDomainDistance = bdd;
            }
            cache[index] = edgeCache;
        }

        public void Merge(MultiDistanceSelector other)
        {
            _r.Merge(other._r);
            _g.Merge(other._g);
            _b.Merge(other._b);
        }

        public MultiDistance Distance()
        {
            return new MultiDistance
            {
                R = _r.ComputeDistance(_p),
                G = _g.ComputeDistance(_p),
                B = _b.ComputeDistance(_p)
            };
        }

        public SignedDistance TrueDistance()
        {
            var d = _r.TrueDistance();
            if (_g.TrueDistance() < d) d = _g.TrueDistance();
            if (_b.TrueDistance() < d) d = _b.TrueDistance();
            return d;
        }
    }

    public struct MultiAndTrueDistanceSelector : IDistanceSelector<MultiAndTrueDistanceSelector, MultiAndTrueDistance>
    {
        private Vector2 _p;
        private PerpendicularDistanceSelectorBase _r = new ();
        private PerpendicularDistanceSelectorBase _g = new ();
        private PerpendicularDistanceSelectorBase _b = new ();

        public MultiAndTrueDistanceSelector()
        {
            
        }

        public void AddEdge(Span<EdgeCache> cache, int index, EdgeSegment prev, EdgeSegment edge, EdgeSegment next)
        {
            EdgeColor color = edge.Color;
            bool doR = (color & EdgeColor.Red) != 0 && _r.IsEdgeRelevant(cache[index], edge, _p);
            bool doG = (color & EdgeColor.Green) != 0 && _g.IsEdgeRelevant(cache[index], edge, _p);
            bool doB = (color & EdgeColor.Blue) != 0 && _b.IsEdgeRelevant(cache[index], edge, _p);
            if (doR || doG || doB)
            {
                float param;
                var dist = edge.SignedDistance(_p, out param);
                if (doR) _r.AddEdgeTrueDistance(edge, dist, param);
                if (doG) _g.AddEdgeTrueDistance(edge, dist, param);
                if (doB) _b.AddEdgeTrueDistance(edge, dist, param);
                cache[index].Point = _p;
                cache[index].AbsDistance = MathF.Abs(dist.Distance);

                Vector2 ap = _p - edge.Point(0);
                Vector2 bp = _p - edge.Point(1);
                Vector2 aDir = edge.Direction(0).Normalize(true);
                Vector2 bDir = edge.Direction(1).Normalize(true);
                Vector2 prevDir = prev.Direction(1).Normalize(true);
                Vector2 nextDir = next.Direction(0).Normalize(true);
                float add = Vector2.Dot(ap, (prevDir + aDir).Normalize(true));
                float bdd = -Vector2.Dot(bp, (bDir + nextDir).Normalize(true));

                if (add > 0)
                {
                    float pd = dist.Distance;
                    if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, ap, -aDir))
                    {
                        pd = -pd;
                        if (doR) _r.AddEdgePerpendicularDistance(pd);
                        if (doG) _g.AddEdgePerpendicularDistance(pd);
                        if (doB) _b.AddEdgePerpendicularDistance(pd);
                    }
                    cache[index].APerpDistance = pd;
                }
                if (bdd > 0)
                {
                    float pd = dist.Distance;
                    if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, bp, bDir))
                    {
                        if (doR) _r.AddEdgePerpendicularDistance(pd);
                        if (doG) _g.AddEdgePerpendicularDistance(pd);
                        if (doB) _b.AddEdgePerpendicularDistance(pd);
                    }
                    cache[index].BPerpDistance = pd;
                }
                cache[index].ADomainDistance = add;
                cache[index].BDomainDistance = bdd;
            }
        }

        public void Reset(Vector2 p)
        {
            float delta = PerpendicularDistanceSelectorBase.DISTANCE_DELTA_FACTOR * (p - _p).Length();
            _r.Reset(delta);
            _g.Reset(delta);
            _b.Reset(delta);
            _p = p;
        }

        public MultiAndTrueDistance Distance()
        {
            var d = _r.TrueDistance();
            if (_g.TrueDistance() < d) d = _g.TrueDistance();
            if (_b.TrueDistance() < d) d = _b.TrueDistance();

            return new MultiAndTrueDistance
            {
                R = _r.ComputeDistance(_p),
                G = _g.ComputeDistance(_p),
                B = _b.ComputeDistance(_p),
                A = d.Distance
            };
        }

        public void Merge(MultiAndTrueDistanceSelector other)
        {
            _r.Merge(other._r);
            _g.Merge(other._g);
            _b.Merge(other._b);
        }

    }
}