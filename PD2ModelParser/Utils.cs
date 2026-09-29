using System;
using System.Collections.Generic;
using System.Numerics;

namespace PD2ModelParser
{
    internal static class Tags
    {
        public const uint animation_data_tag = 0x5DC011B8; // Animation data
        public const uint author_tag = 0x7623C465; // Author tag
        public const uint material_group_tag = 0x29276B1D; // Material Group
        public const uint material_tag = 0x3C54609C; // Material
        public const uint object3D_tag = 0x0FFCD100; // Object3D
        public const uint model_data_tag = 0x62212D88; // Model data
        public const uint geometry_tag = 0x7AB072D3; // DieselGeometry
        public const uint topology_tag = 0x4C507A13; // Topology
        public const uint passthroughGP_tag = 0xE3A3B1CA; // PassthroughGP
        public const uint topologyIP_tag = 0x03B634BD; // TopologyIP
        public const uint quatLinearRotationController_tag = 0x648A206C; // QuatLinearRotationController
        public const uint quatBezRotationController_tag = 0x197345A5; // QuatBezRotationController
        public const uint skinbones_tag = 0x65CC1825; // SkinBones
        public const uint bones_tag = 0xEB43C77; // Bones
        public const uint light_tag = 0xFFA13B80; //Light
        public const uint lightSet_tag = 0x33552583; //LightSet
        public const uint linearVector3Controller_tag = 0x26A5128C; //LinearVector3Controller
        public const uint linearFloatController_tag = 0x76BF5B66; //LinearFloatController
        public const uint lookAtConstrRotationController = 0x679D695B; //LookAtConstrRotationController
        public const uint camera_tag = 0x46BF31A7; //Camera

        // Custom tags - not used in vanilla PD2, but used here for whatever reason
        // To generate these on Linux, use xxd -l 4 /dev/random
        public const uint custom_hashlist_tag = 0x7c7844fd;
    }

    internal static class MathUtil
    {

        public static Vector4 ToVector4(this Sections.GeometryColor input) => new(input.red/255.0f, input.green/255.0f, input.blue/255.0f, input.alpha/255.0f);

        public static Sections.GeometryColor ToGeometryColor(this Vector4 input) {
            return new Sections.GeometryColor(
                ClampFloatToByte(input.X),
                ClampFloatToByte(input.Y),
                ClampFloatToByte(input.Z),
                ClampFloatToByte(input.W));
        }
        
        public static Vector3 Max(Vector3 left, Vector3 right) => new(Math.Max(left.X, right.X), Math.Max(left.Y, right.Y), Math.Max(left.Z, right.Z));
        public static Vector3 Min(Vector3 left, Vector3 right) => new(Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));

        /// <summary>
        /// Clamp a float to 0..1 and rescale it to 0..255
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte ClampFloatToByte(double input)
        {
            input = Math.Clamp(input, 0.0, 1.0);
            var scaled = input * 255;
            var rounded = Math.Round(scaled);
            return (byte)rounded;
        }

        public static Boolean IsFinite(this System.Numerics.Vector3 vec)
        {
            return !(
                float.IsInfinity(vec.X)
                || float.IsInfinity(vec.Y)
                || float.IsInfinity(vec.Z)
                || float.IsNaN(vec.X)
                || float.IsNaN(vec.Y)
                || float.IsNaN(vec.Z)
            );
        }

        // From https://github.com/KhronosGroup/glTF-Validator/blob/master/lib/src/errors.dart
        // which says, "these values are slightly greater than the maximum error from signed 8-bit quantization"
        private const float UnitLengthThresholdVec3 = 0.00674f;

        public static Boolean IsUnitLength(this System.Numerics.Vector3 vec) =>
            Math.Abs(vec.Length() - 1) <= UnitLengthThresholdVec3;


    }


    public static class MatrixExtensions
    {
        public static ref float Index(ref this Matrix4x4 @this, int idx)
        {
            switch(idx)
            {
                case 0 : return ref @this.M11;
                case 1 : return ref @this.M12;
                case 2 : return ref @this.M13;
                case 3 : return ref @this.M14;
                case 4 : return ref @this.M21;
                case 5 : return ref @this.M22;
                case 6 : return ref @this.M23;
                case 7 : return ref @this.M24;
                case 8 : return ref @this.M31;
                case 9 : return ref @this.M32;
                case 10: return ref @this.M33;
                case 11: return ref @this.M34;
                case 12: return ref @this.M41;
                case 13: return ref @this.M42;
                case 14: return ref @this.M43;
                case 15: return ref @this.M44;
                default: throw new IndexOutOfRangeException("Invalid matrix index!");
            }
        }

        public static ref float Index(ref this Matrix4x4 @this, int c, int r) => ref @this.Index(r * 4 + c);


        /**
         * Multiply two vectors the same way Diesel does. This is a bit confusing, see the decompiled
         * code in 'decompiled matrix parsing' and 'decompiled matrix parsing 2' (in the
         * Research Notes directory). This is much cleaned up code originally produced by HexRays
         * from dsl::SkinBones::post_load in the GNU+Linux binary.
         *
         * This has been tested against the PAYDAY 2 GNU+Linux binary, and in all cases produced the correct
         * results. To repeat this, I'd recommend breaking after the first _mm_load_si128 call in
         * dsl::SkinBones::post_load and reading $xmm0 through $xmm3 (or $rsi+0xE0 through $rsi+0x110), which
         * represent the output values of the multiplication (originally performed in dsl::Object3D::post_load).
         * The input values are stored at $rsi+0xA0 through $rsi+0xD0, and the others are stored in the parent object
         * pointed to by $rsi+0x130, whose values are at 0xE0 through 0x110.
         *
         * In any case, this was an awful lot of work.
         *
         * See https://github.com/blt4linux/research for the relevant headers.
         */
        public static Matrix4x4 MultDiesel(this Matrix4x4 a, Matrix4x4 b)
        {
            // Check the Git history if you're curious as to how we ended up with a function for this. I think it all
            // un-broke itself when moving to the System.Numerics library.
            return a * b;
        }
    }

    [System.ComponentModel.TypeConverter(typeof(Inspector.HashNameConverter))]
    public class HashName
    {
        private ulong hash;
        private string str;
        private readonly bool known;
    
        [System.ComponentModel.NotifyParentProperty(true)]
        public string String
        {
            get => str ?? StaticStorage.hashindex.GetString(hash);
            set
            {
                str = value ?? throw new ArgumentNullException(nameof(value));
                hash = Hash64.HashString(value);
            }
        }

        public override string ToString() => this.String;

        [System.ComponentModel.NotifyParentProperty(true)]
        public ulong Hash
        {
            get => hash;
            set
            {
                if (hash == value)
                    return;

                hash = value;
                str = null;
            }
        }

        public bool Known => known || StaticStorage.hashindex.Contains(Hash);

        public HashName(string str)
        {
            this.str = str ?? throw new ArgumentNullException(nameof(str));
            hash = Hash64.HashString(str);
            known = true;
        }

        public HashName(ulong hash)
        {
            this.hash = hash;
            str = null;
            known = false;
        }

        public static HashName FromNumberOrString(string input)
        {
            if (ulong.TryParse(input, out ulong hashnum))
            {
                return new HashName(hashnum);
            }
            else
            {
                return new HashName(input);
            }
        }
    }

    internal static class MiscUtil
    {

        public static void WithValue<T>(this T? self, Action<T> cb) where T: struct
        {
            if (self.HasValue) { cb(self.Value); }
        }

        public static IEnumerable<T> OrderedDistinct<T>(this IEnumerable<T> self)
        {
            var set = new HashSet<T>();
            foreach(var i in self)
            {
                if(set.Add(i))
                {
                    yield return i;
                    set.Add(i);
                }
            }
        }


    }

    
}
