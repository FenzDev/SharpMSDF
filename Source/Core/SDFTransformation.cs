using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Core
{
    public struct SDFTransformation
    {
        public Projection Projection;
        public DistanceMapping DistanceMapping;

        public SDFTransformation() {
            
            Projection = new();
            DistanceMapping = new();
        }

        public SDFTransformation(Projection projection, DistanceMapping distanceMapping)
        {
            Projection = projection;
            DistanceMapping = distanceMapping;
        }
    }
}
