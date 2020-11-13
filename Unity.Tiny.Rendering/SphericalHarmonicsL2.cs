using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Tiny.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SHCoefficients
    {
        public float4 SHAr;
        public float4 SHAg;
        public float4 SHAb;
        public float4 SHBr;
        public float4 SHBg;
        public float4 SHBb;
        public float4 SHC;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SphericalHarmonicsL2
    {
        private float shr0, shr1, shr2, shr3, shr4, shr5, shr6, shr7, shr8;
        private float shg0, shg1, shg2, shg3, shg4, shg5, shg6, shg7, shg8;
        private float shb0, shb1, shb2, shb3, shb4, shb5, shb6, shb7, shb8;

        public float this[int rgb, int coefficient]
        {
            get
            {
                int idx = rgb * 9 + coefficient;
                switch (idx)
                {
                    case 0: return shr0;
                    case 1: return shr1;
                    case 2: return shr2;
                    case 3: return shr3;
                    case 4: return shr4;
                    case 5: return shr5;
                    case 6: return shr6;
                    case 7: return shr7;
                    case 8: return shr8;
                    case 9: return shg0;
                    case 10: return shg1;
                    case 11: return shg2;
                    case 12: return shg3;
                    case 13: return shg4;
                    case 14: return shg5;
                    case 15: return shg6;
                    case 16: return shg7;
                    case 17: return shg8;
                    case 18: return shb0;
                    case 19: return shb1;
                    case 20: return shb2;
                    case 21: return shb3;
                    case 22: return shb4;
                    case 23: return shb5;
                    case 24: return shb6;
                    case 25: return shb7;
                    case 26: return shb8;
                    default:
                        throw new IndexOutOfRangeException("Invalid index!");
                }
            }

            set
            {
                int idx = rgb * 9 + coefficient;
                switch (idx)
                {
                    case 0: shr0 = value; break;
                    case 1: shr1 = value; break;
                    case 2: shr2 = value; break;
                    case 3: shr3 = value; break;
                    case 4: shr4 = value; break;
                    case 5: shr5 = value; break;
                    case 6: shr6 = value; break;
                    case 7: shr7 = value; break;
                    case 8: shr8 = value; break;
                    case 9: shg0 = value; break;
                    case 10: shg1 = value; break;
                    case 11: shg2 = value; break;
                    case 12: shg3 = value; break;
                    case 13: shg4 = value; break;
                    case 14: shg5 = value; break;
                    case 15: shg6 = value; break;
                    case 16: shg7 = value; break;
                    case 17: shg8 = value; break;
                    case 18: shb0 = value; break;
                    case 19: shb1 = value; break;
                    case 20: shb2 = value; break;
                    case 21: shb3 = value; break;
                    case 22: shb4 = value; break;
                    case 23: shb5 = value; break;
                    case 24: shb6 = value; break;
                    case 25: shb7 = value; break;
                    case 26: shb8 = value; break;
                    default:
                        throw new IndexOutOfRangeException("Invalid index!");
                }
            }
        }

        public void Clear()
        {
            unsafe
            {
                fixed (void* p = &shr0)
                    UnsafeUtility.MemSet(p, 0, UnsafeUtility.SizeOf<SphericalHarmonicsL2>());
            }
        }

        public void AddAmbientLight(float3 color)
        {
            shr0 += color.x;
            shg0 += color.y;
            shb0 += color.z;
        }

        public void AddDirectionalLight(float3 direction, float3 color)
        {
            float4 evaluatedDirection0;
            float4 evaluatedDirection1;
            float4 evaluatedDirection2;
            SHEvalDirection9(direction, out evaluatedDirection0, out evaluatedDirection1, out evaluatedDirection2);

            // Normalization factor from http://www.ppsloan.org/publications/StupidSH36.pdf
            float kNormalization = 2.9567930857315701067858823529412f; // 16*kPI/17

            float3 scaled = color * kNormalization;

            float kInv2SqrtPI = 0.28209479177387814347403972578039f; // 1 / (2*sqrt(kPI))
            float kSqrt3Div3SqrtPI = 0.32573500793527994772f; // sqrt(3) / (3*sqrt(kPI))
            float kSqrt15Div8SqrtPI = 0.27313710764801976764f; // sqrt(15) / (8*sqrt(kPI))
            float kSqrt5Div16SqrtPI = 0.13656855382400988382f; // sqrt(15) / (4 * sqrt(kPI))

            evaluatedDirection0 *= new float4(kInv2SqrtPI, -kSqrt3Div3SqrtPI, kSqrt3Div3SqrtPI, -kSqrt3Div3SqrtPI);
            evaluatedDirection1 *= new float4(kSqrt15Div8SqrtPI, -kSqrt15Div8SqrtPI, kSqrt5Div16SqrtPI, -kSqrt15Div8SqrtPI);
            evaluatedDirection2.x *= 0.5f * kSqrt15Div8SqrtPI;

            AddToCoefficients(evaluatedDirection0, evaluatedDirection1, evaluatedDirection2, scaled);
        }

        public override int GetHashCode()
        {
            // Hash code idea from http://stackoverflow.com/a/263416

            unchecked
            { // // Overflow is fine, just wrap
                int hash = 17;
                hash = hash * 23 + shr0.GetHashCode();
                hash = hash * 23 + shr1.GetHashCode();
                hash = hash * 23 + shr2.GetHashCode();
                hash = hash * 23 + shr3.GetHashCode();
                hash = hash * 23 + shr4.GetHashCode();
                hash = hash * 23 + shr5.GetHashCode();
                hash = hash * 23 + shr6.GetHashCode();
                hash = hash * 23 + shr7.GetHashCode();
                hash = hash * 23 + shr8.GetHashCode();
                hash = hash * 23 + shg0.GetHashCode();
                hash = hash * 23 + shg1.GetHashCode();
                hash = hash * 23 + shg2.GetHashCode();
                hash = hash * 23 + shg3.GetHashCode();
                hash = hash * 23 + shg4.GetHashCode();
                hash = hash * 23 + shg5.GetHashCode();
                hash = hash * 23 + shg6.GetHashCode();
                hash = hash * 23 + shg7.GetHashCode();
                hash = hash * 23 + shg8.GetHashCode();
                hash = hash * 23 + shb0.GetHashCode();
                hash = hash * 23 + shb1.GetHashCode();
                hash = hash * 23 + shb2.GetHashCode();
                hash = hash * 23 + shb3.GetHashCode();
                hash = hash * 23 + shb4.GetHashCode();
                hash = hash * 23 + shb5.GetHashCode();
                hash = hash * 23 + shb6.GetHashCode();
                hash = hash * 23 + shb7.GetHashCode();
                hash = hash * 23 + shb8.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object other)
        {
            return other is SphericalHarmonicsL2 && Equals((SphericalHarmonicsL2)other);
        }

        public bool Equals(SphericalHarmonicsL2 other)
        {
            return this == other;
        }

        static public SphericalHarmonicsL2 operator *(SphericalHarmonicsL2 lhs, float rhs)
        {
            SphericalHarmonicsL2 r = new SphericalHarmonicsL2();
            r.shr0 = lhs.shr0 * rhs;
            r.shr1 = lhs.shr1 * rhs;
            r.shr2 = lhs.shr2 * rhs;
            r.shr3 = lhs.shr3 * rhs;
            r.shr4 = lhs.shr4 * rhs;
            r.shr5 = lhs.shr5 * rhs;
            r.shr6 = lhs.shr6 * rhs;
            r.shr7 = lhs.shr7 * rhs;
            r.shr8 = lhs.shr8 * rhs;
            r.shg0 = lhs.shg0 * rhs;
            r.shg1 = lhs.shg1 * rhs;
            r.shg2 = lhs.shg2 * rhs;
            r.shg3 = lhs.shg3 * rhs;
            r.shg4 = lhs.shg4 * rhs;
            r.shg5 = lhs.shg5 * rhs;
            r.shg6 = lhs.shg6 * rhs;
            r.shg7 = lhs.shg7 * rhs;
            r.shg8 = lhs.shg8 * rhs;
            r.shb0 = lhs.shb0 * rhs;
            r.shb1 = lhs.shb1 * rhs;
            r.shb2 = lhs.shb2 * rhs;
            r.shb3 = lhs.shb3 * rhs;
            r.shb4 = lhs.shb4 * rhs;
            r.shb5 = lhs.shb5 * rhs;
            r.shb6 = lhs.shb6 * rhs;
            r.shb7 = lhs.shb7 * rhs;
            r.shb8 = lhs.shb8 * rhs;
            return r;
        }

        static public SphericalHarmonicsL2 operator *(float lhs, SphericalHarmonicsL2 rhs)
        {
            SphericalHarmonicsL2 r = new SphericalHarmonicsL2();
            r.shr0 = rhs.shr0 * lhs;
            r.shr1 = rhs.shr1 * lhs;
            r.shr2 = rhs.shr2 * lhs;
            r.shr3 = rhs.shr3 * lhs;
            r.shr4 = rhs.shr4 * lhs;
            r.shr5 = rhs.shr5 * lhs;
            r.shr6 = rhs.shr6 * lhs;
            r.shr7 = rhs.shr7 * lhs;
            r.shr8 = rhs.shr8 * lhs;
            r.shg0 = rhs.shg0 * lhs;
            r.shg1 = rhs.shg1 * lhs;
            r.shg2 = rhs.shg2 * lhs;
            r.shg3 = rhs.shg3 * lhs;
            r.shg4 = rhs.shg4 * lhs;
            r.shg5 = rhs.shg5 * lhs;
            r.shg6 = rhs.shg6 * lhs;
            r.shg7 = rhs.shg7 * lhs;
            r.shg8 = rhs.shg8 * lhs;
            r.shb0 = rhs.shb0 * lhs;
            r.shb1 = rhs.shb1 * lhs;
            r.shb2 = rhs.shb2 * lhs;
            r.shb3 = rhs.shb3 * lhs;
            r.shb4 = rhs.shb4 * lhs;
            r.shb5 = rhs.shb5 * lhs;
            r.shb6 = rhs.shb6 * lhs;
            r.shb7 = rhs.shb7 * lhs;
            r.shb8 = rhs.shb8 * lhs;
            return r;
        }

        static public SphericalHarmonicsL2 operator +(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
        {
            SphericalHarmonicsL2 r = new SphericalHarmonicsL2();
            r.shr0 = lhs.shr0 + rhs.shr0;
            r.shr1 = lhs.shr1 + rhs.shr1;
            r.shr2 = lhs.shr2 + rhs.shr2;
            r.shr3 = lhs.shr3 + rhs.shr3;
            r.shr4 = lhs.shr4 + rhs.shr4;
            r.shr5 = lhs.shr5 + rhs.shr5;
            r.shr6 = lhs.shr6 + rhs.shr6;
            r.shr7 = lhs.shr7 + rhs.shr7;
            r.shr8 = lhs.shr8 + rhs.shr8;
            r.shg0 = lhs.shg0 + rhs.shg0;
            r.shg1 = lhs.shg1 + rhs.shg1;
            r.shg2 = lhs.shg2 + rhs.shg2;
            r.shg3 = lhs.shg3 + rhs.shg3;
            r.shg4 = lhs.shg4 + rhs.shg4;
            r.shg5 = lhs.shg5 + rhs.shg5;
            r.shg6 = lhs.shg6 + rhs.shg6;
            r.shg7 = lhs.shg7 + rhs.shg7;
            r.shg8 = lhs.shg8 + rhs.shg8;
            r.shb0 = lhs.shb0 + rhs.shb0;
            r.shb1 = lhs.shb1 + rhs.shb1;
            r.shb2 = lhs.shb2 + rhs.shb2;
            r.shb3 = lhs.shb3 + rhs.shb3;
            r.shb4 = lhs.shb4 + rhs.shb4;
            r.shb5 = lhs.shb5 + rhs.shb5;
            r.shb6 = lhs.shb6 + rhs.shb6;
            r.shb7 = lhs.shb7 + rhs.shb7;
            r.shb8 = lhs.shb8 + rhs.shb8;
            return r;
        }

        public static bool operator ==(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
        {
            return
                lhs.shr0 == rhs.shr0 &&
                lhs.shr1 == rhs.shr1 &&
                lhs.shr2 == rhs.shr2 &&
                lhs.shr3 == rhs.shr3 &&
                lhs.shr4 == rhs.shr4 &&
                lhs.shr5 == rhs.shr5 &&
                lhs.shr6 == rhs.shr6 &&
                lhs.shr7 == rhs.shr7 &&
                lhs.shr8 == rhs.shr8 &&
                lhs.shg0 == rhs.shg0 &&
                lhs.shg1 == rhs.shg1 &&
                lhs.shg2 == rhs.shg2 &&
                lhs.shg3 == rhs.shg3 &&
                lhs.shg4 == rhs.shg4 &&
                lhs.shg5 == rhs.shg5 &&
                lhs.shg6 == rhs.shg6 &&
                lhs.shg7 == rhs.shg7 &&
                lhs.shg8 == rhs.shg8 &&
                lhs.shb0 == rhs.shb0 &&
                lhs.shb1 == rhs.shb1 &&
                lhs.shb2 == rhs.shb2 &&
                lhs.shb3 == rhs.shb3 &&
                lhs.shb4 == rhs.shb4 &&
                lhs.shb5 == rhs.shb5 &&
                lhs.shb6 == rhs.shb6 &&
                lhs.shb7 == rhs.shb7 &&
                lhs.shb8 == rhs.shb8;
        }

        public static bool operator !=(SphericalHarmonicsL2 lhs, SphericalHarmonicsL2 rhs)
        {
            return !(lhs == rhs);
        }

        public SHCoefficients ToSHCoefficients()
        {
            var result = new SHCoefficients();

            // Constant (DC terms):
            result.SHAr.w = this[0, 0];
            result.SHAg.w = this[1, 0];
            result.SHAb.w = this[2, 0];

            // Linear: (used by L1 and L2)
            // Swizzle the coefficients to be in { x, y, z } order.
            result.SHAr.x = this[0, 3];
            result.SHAr.y = this[0, 1];
            result.SHAr.z = this[0, 2];

            result.SHAg.x = this[1, 3];
            result.SHAg.y = this[1, 1];
            result.SHAg.z = this[1, 2];

            result.SHAb.x = this[2, 3];
            result.SHAb.y = this[2, 1];
            result.SHAb.z = this[2, 2];

            // Quadratic: (used by L2)
            result.SHBr.x = this[0, 4];
            result.SHBr.y = this[0, 5];
            result.SHBr.z = this[0, 6];
            result.SHBr.w = this[0, 7];

            result.SHBg.x = this[1, 4];
            result.SHBg.y = this[1, 5];
            result.SHBg.z = this[1, 6];
            result.SHBg.w = this[1, 7];

            result.SHBb.x = this[2, 4];
            result.SHBb.y = this[2, 5];
            result.SHBb.z = this[2, 6];
            result.SHBb.w = this[2, 7];

            result.SHC.x = this[0, 8];
            result.SHC.y = this[1, 8];
            result.SHC.z = this[2, 8];

            return result;
        }

        static void SHEvalDirection9(float3 v, out float4 outsh0, out float4 outsh1, out float4 outsh2)
        {
            float kInv2SqrtPI = 0.28209479177387814347403972578039f; // 1 / (2*sqrt(kPI))
            float kSqrt3Div2SqrtPI = 0.48860251190291992158638462283835f; // sqrt(3) / (2*sqrt(kPI))
            float kSqrt15Div2SqrtPI = 1.0925484305920790705433857058027f; // sqrt(15) / (2*sqrt(kPI))
            float k3Sqrt5Div4SqrtPI = 0.94617469575756001809268107088713f; // 3 * sqrtf(5) / (4*sqrt(kPI))
            float kSqrt15Div4SqrtPI = 0.54627421529603953527169285290135f; // sqrt(15) / (4*sqrt(kPI))
            float kOneThird = 0.3333333333333333333333f; // 1.0/3.0

            float4 kMul0 = new float4(kInv2SqrtPI, kSqrt3Div2SqrtPI, kSqrt3Div2SqrtPI, kSqrt3Div2SqrtPI);
            float3 vsq = v * v;
            float4 mul0 = new float4(1.0f, -v.y, v.z, -v.x);
            outsh0 = mul0 * kMul0;

            float4 kMul1 = new float4(kSqrt15Div2SqrtPI, kSqrt15Div2SqrtPI, k3Sqrt5Div4SqrtPI, kSqrt15Div2SqrtPI);
            float4 mul1 = new float4(v.x * v.y, -v.y * v.z, vsq.z - kOneThird, -v.x * v.z);
            outsh1 = mul1 * kMul1;

            outsh2 = (vsq.x - vsq.y) * kSqrt15Div4SqrtPI;
        }

        void AddToCoefficients(in float4 evaluatedDirection0, in float4 evaluatedDirection1, in float4 evaluatedDirection2, float3 rgb)
        {
            {
                var p0 = new float4(shr0, shr1, shr2, shr3) + (evaluatedDirection0 * rgb.x);
                var p1 = new float4(shr4, shr5, shr6, shr7) + (evaluatedDirection1 * rgb.x);
                shr0 = p0.x;
                shr1 = p0.y;
                shr2 = p0.z;
                shr3 = p0.w;
                shr4 = p1.x;
                shr5 = p1.y;
                shr6 = p1.z;
                shr7 = p1.w;
                shr8 += (evaluatedDirection2.x * rgb.x);
            }

            {
                var p0 = new float4(shg0, shg1, shg2, shg3) + (evaluatedDirection0 * rgb.y);
                var p1 = new float4(shg4, shg5, shg6, shg7) + (evaluatedDirection1 * rgb.y);
                shg0 = p0.x;
                shg1 = p0.y;
                shg2 = p0.z;
                shg3 = p0.w;
                shg4 = p1.x;
                shg5 = p1.y;
                shg6 = p1.z;
                shg7 = p1.w;
                shg8 += (evaluatedDirection2.x * rgb.y);
            }

            {
                var p0 = new float4(shb0, shb1, shb2, shb3) + (evaluatedDirection0 * rgb.z);
                var p1 = new float4(shb4, shb5, shb6, shb7) + (evaluatedDirection1 * rgb.z);
                shb0 = p0.x;
                shb1 = p0.y;
                shb2 = p0.z;
                shb3 = p0.w;
                shb4 = p1.x;
                shb5 = p1.y;
                shb6 = p1.z;
                shb7 = p1.w;
                shb8 += (evaluatedDirection2.x * rgb.y);
            }
        }
    }
}
