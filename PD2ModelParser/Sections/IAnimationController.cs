using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PD2ModelParser.Sections
{
    public interface IAnimationController : ISection, IHashNamed
    {
        uint Flags { get; set; }
        float KeyframeLength { get; set; }
    }

    public interface IAnimationController<TValue> : IAnimationController
    {
        IList<Keyframe<TValue>> Keyframes { get; set; }
    }

    public class Keyframe<T>(float ts, T v)
    {
        public float Timestamp { get; set; } = ts;
        public T Value { get; set; } = v;

        public override string ToString() => $"Timestamp={Timestamp} Value={Value}";
    }
}
