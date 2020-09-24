using Unity.Tiny.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Unity.Tiny.Rendering
{
    public static class MeshHelper
    {
        public static AABB ComputeBounds(NativeArray<LitVertex> vertices)
        {
            if (vertices.Length <= 0)
                return new AABB();
            float3 bbMin = vertices[0].Position;
            float3 bbMax = bbMin;
            for (int i = 1; i < vertices.Length; i++)
            {
                bbMin = math.min(vertices[i].Position, bbMin);
                bbMax = math.max(vertices[i].Position, bbMax);
            }
            return new AABB { Center = (bbMin + bbMax) * .5f, Extents = (bbMax - bbMin) * .5f };
        }

        public static AABB ComputeBounds(NativeArray<SimpleVertex> vertices)
        {
            if (vertices.Length <= 0)
                return new AABB();
            float3 bbMin = vertices[0].Position;
            float3 bbMax = bbMin;
            for (int i = 1; i < vertices.Length; i++)
            {
                bbMin = math.min(vertices[i].Position, bbMin);
                bbMax = math.max(vertices[i].Position, bbMax);
            }
            return new AABB { Center = (bbMin + bbMax) * .5f, Extents = (bbMax - bbMin) * .5f };
        }

        public static void ComputeNormals(NativeArray<LitVertex> vertices, NativeArray<ushort> indices, bool clear = false)
        {
            unsafe {
                int nv = vertices.Length;
                var verts = (LitVertex*)vertices.GetUnsafePtr();
                int ni = indices.Length;
                var inds = (ushort*)indices.GetUnsafePtr();
                if (clear)
                {
                    for (int i = 0; i < nv; i++)
                        verts[i].Normal = new float3(0);
                }
                for (int i = 0; i < ni; i += 3)
                {
                    int i0 = inds[i];
                    int i1 = inds[i + 1];
                    int i2 = inds[i + 2];
                    Assert.IsTrue(i0 >= 0 && i0 < nv && i1 >= 0 && i1 < nv && i2 >= 0 && i2 < nv);
                    float3 n = -math.cross(verts[i2].Position - verts[i0].Position, verts[i1].Position - verts[i0].Position); // don't normalize, weights by area
                    Assert.IsTrue(math.lengthsq(n) > 0.0f); // zero sized triangles are bad
                    verts[i0].Normal += n;
                    verts[i1].Normal += n;
                    verts[i2].Normal += n;
                }
                for (int i = 0; i < nv; i++)
                {
                    Assert.IsTrue(math.lengthsq(verts[i].Normal) > 0.0f);
                    verts[i].Normal = math.normalize(verts[i].Normal);
                }
            }
        }

        public static void ComputeTangentAndBinormal(NativeArray<LitVertex> vertices, NativeArray<ushort> indices)
        {
            unsafe {
                int nv = vertices.Length;
                int ni = indices.Length;
                ushort* inds = (ushort*)indices.GetUnsafePtr();
                LitVertex* verts = (LitVertex*)vertices.GetUnsafePtr();

                // assumes normal is valid!
                for (int i = 0; i < ni; i += 3)
                {
                    int i0 = inds[i + 2];
                    int i1 = inds[i + 1];
                    int i2 = inds[i];
                    Assert.IsTrue(i0 >= 0 && i0 < nv && i1 >= 0 && i1 < nv && i2 >= 0 && i2 < nv);
                    float3 edge1 = verts[i1].Position - verts[i0].Position;
                    Assert.IsTrue(math.lengthsq(edge1) > 0);
                    float3 edge2 = verts[i2].Position - verts[i0].Position;
                    Assert.IsTrue(math.lengthsq(edge2) > 0);
                    float2 uv1 = verts[i1].TexCoord0 - verts[i0].TexCoord0;
                    Assert.IsTrue(math.lengthsq(uv1) > 0);
                    float2 uv2 = verts[i2].TexCoord0 - verts[i0].TexCoord0;
                    Assert.IsTrue(math.lengthsq(uv2) > 0);
                    float r = 1.0f / (uv1.x * uv2.y - uv1.y * uv2.x);
                    float3 n = math.cross(edge2, edge1);
                    float3 tangent = new float3(
                        ((edge1.x * uv2.y) - (edge2.x * uv1.y)) * r,
                        ((edge1.y * uv2.y) - (edge2.y * uv1.y)) * r,
                        ((edge1.z * uv2.y) - (edge2.z * uv1.y)) * r
                    );
                    float3 bitangent = new float3(
                        ((edge1.x * uv2.x) - (edge2.x * uv1.x)) * r,
                        ((edge1.y * uv2.x) - (edge2.y * uv1.x)) * r,
                        ((edge1.z * uv2.x) - (edge2.z * uv1.x)) * r
                    );
                    Assert.IsTrue(math.lengthsq(tangent) > 0.0f);
                    Assert.IsTrue(math.lengthsq(bitangent) > 0.0f);
                    float3 n2 = math.cross(tangent, bitangent);
                    if (math.dot(n2, n) > 0.0f)
                    {
                        tangent = -tangent;
                    }
                    verts[i0].Tangent += tangent;
                    verts[i1].Tangent += tangent;
                    verts[i2].Tangent += tangent;
                }

                for (int i = 0; i < nv; i++)
                {
                    Assert.IsTrue(math.lengthsq(verts[i].Tangent) > 0.0f);
                    verts[i].Tangent = math.normalize(verts[i].Tangent);
                }
            }
        }

        private static void AddBoxFace(NativeArray<LitVertex> vertices, NativeArray<ushort> indices, int side, int sign, float3 size, ref int destI, ref int destV, float uvscale)
        {
            int i0 = destV;
            float3 p = new float3(0);
            p[side] = sign * size[side];
            float3 du = new float3(0);
            int side1 = (side + 1) % 3;
            du[side1] = sign * size[side1];
            float3 dv = new float3(0);
            int side2 = (side + 2) % 3;
            dv[side2] = 1.0f * size[side2];
            float3 p0 = -du - dv + p; float2 uv0 = new float2(0, 0.0f);
            float3 p1 = du - dv + p; float2 uv1 = new float2(size[side1] * uvscale, 0.0f);
            float3 p2 = du + dv + p; float2 uv2 = new float2(size[side1] * uvscale, size[side2] * uvscale);
            float3 p3 = -du + dv + p; float2 uv3 = new float2(0, size[side2] * uvscale);
            vertices[destV] = new LitVertex { Position = p0, TexCoord0 = uv0 }; destV++;
            vertices[destV] = new LitVertex { Position = p1, TexCoord0 = uv1 }; destV++;
            vertices[destV] = new LitVertex { Position = p2, TexCoord0 = uv2 }; destV++;
            vertices[destV] = new LitVertex { Position = p3, TexCoord0 = uv3 }; destV++;
            indices[destI + 2] = (ushort)i0; indices[destI + 1] = (ushort)(i0 + 2); indices[destI] = (ushort)(i0 + 1);
            indices[destI + 5] = (ushort)(i0 + 2); indices[destI + 4] = (ushort)(i0 + 0); indices[destI + 3] = (ushort)(i0 + 3);
            destI += 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ESin(float x, float e)
        {
            float sx = math.sin(x);
            float y = math.pow(math.abs(sx), e);
            return sx < 0.0f ? -y : y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ECos(float x, float e)
        {
            float cx = math.cos(x);
            float y = math.pow(math.abs(cx), e);
            return cx < 0.0f ? -y : y;
        }

        // phi is between 0..2*pi
        public static float3 EvalSuperTorusKnotCenterLine(float phi, int p, int q, float e)
        {
            float r = ECos(q * phi, e) + 2.0f;
            return new float3(
                r * ECos(p * phi, e),
                r * ESin(p * phi, e),
                -ESin(q * phi, e)
            ) * (1.0f / 3.0f);
            /*
            float r = .5f * ( ESin(q*phi, e) + 2.0f );
            return new float3 (
                r * ECos(p*phi, e),
                r * ESin(p*phi, e),
                r * ECos(q*phi, e)
                );*/
        }

        // phi is between 0..2*pi
        public static float2 EvalSuperCircle(float phi, float e)
        {
            return new float2(ESin(phi, e), ECos(phi, e));
        }

        // makes a super torus knot.
        // the knot part is based on: https://en.wikipedia.org/wiki/Torus_knot and https://blackpawn.com/texts/pqtorus/
        // and the super part on: https://en.wikipedia.org/wiki/Supertoroid
        // for a regular, round torus use: innerE=outerE=1, and p=1, 1=0
        // good exponents range from around 0.2 ... 2, but can be anuthing >0 and define how aquare the part is
        // p and q are the integer knot parameters
        public static void FillSuperTorusKnot(NativeArray<LitVertex> vertices, NativeArray<ushort> indices,
            float innerR, int innerN, float outerR, int outerN, int p, int q, float innerE, float outerE)
        {
            // profile
            NativeArray<float2> profile = new NativeArray<float2>(innerN, Allocator.TempJob);
            for (int i = 0; i < innerN; i++)
            {
                float fInner = i / (float)(innerN - 1);
                float v = fInner * 2.0f * math.PI;
                profile[i] = EvalSuperCircle(v, innerE);
            }
            // centerline
            NativeArray<float3> centerLine = new NativeArray<float3>(outerN, Allocator.TempJob);
            for (int i = 0; i < outerN; i++)
            {
                float fOuter = i / (float)(outerN - 1); // last vertex is repeated, we have an uv seam there
                centerLine[i] = EvalSuperTorusKnotCenterLine(fOuter * math.PI * 2.0f, p, q, outerE);
            }
            // taper
            NativeArray<float> taper = new NativeArray<float>(2, Allocator.TempJob);
            taper[0] = 0;
            taper[1] = 1;
            centerLine[outerN - 1] = centerLine[0];
            FillExtrudedLine(vertices, indices, innerR, 0, 2.0f * math.PI * innerR, centerLine, profile, taper, true);
            centerLine.Dispose();
            profile.Dispose();
            taper.Dispose();
        }

        public static void FillDonut(NativeArray<LitVertex> vertices, NativeArray<ushort> indices,
            float innerR, int innerN, float outerR, int outerN)
        {
            FillSuperTorusKnot(vertices, indices, innerR, innerN, outerR, outerN, 1, 0, 1, 1);
        }

        public static float3x4 InitCatmullRom(float3 p0, float3 p1, float3 p2, float3 p3)
        {
            return new float3x4(
                p1,
                (p2 - p0) * .5f,
                p0 - p1 * 2.5f + p2 * 2.0f - p3 * .5f,
                (p3 - p0) * .5f + (p1 - p2) * 1.5f
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 EvalCatmullRom(in float3x4 m, float t)
        {
            return m.c0 + (m.c1 + (m.c2 + m.c3 * t) * t) * t;
        }

        private static float ResampleCatmullRomSegment(float3 p0, float3 p1, float3 p2, float3 p3, float preStep, float stepDistance, NativeList<float3> dest)
        {
            float ll = math.length(p1 - p2);
            if (!(ll > stepDistance))
                return ResampleLinearSegment(p1, p2, preStep, stepDistance, dest);
            // subdivide to a very rough aproximation of length, then resample linear from there
            float3x4 spline = InitCatmullRom(p0, p1, p2, p3);
            int k = ((int)(ll / stepDistance) + 1) * 2;
            float3 pPrev = p1; //==EvalCatmullRom(in spline, 0);
            for (int i = 1; i <= k; i++)
            {
                float3 pNext = EvalCatmullRom(in spline, i / (float)k);
                preStep = ResampleLinearSegment(pPrev, pNext, preStep, stepDistance, dest);
                pPrev = pNext;
            }
            return preStep;
        }

        public static NativeList<float3> ResampleCatmullRom(NativeArray<float3> curve, float stepDistance, bool closed, Allocator allocator)
        {
            int n = curve.Length;
            if (n <= 2)
                return ResampleLinear(curve, stepDistance, allocator);
            Assert.IsTrue(n >= 3);
            var result = new NativeList<float3>(n * 2, allocator);
            float preStep = 0;
            for (int i = 0; i < n - 1; i++)
            {
                float3 p1 = curve[i];
                float3 p2 = curve[i + 1];
                float3 p0, p3;
                if (i > 0)
                {
                    p0 = curve[i - 1];
                }
                else
                {
                    if (closed) p0 = curve[n - 1];
                    else p0 = curve[0] + (curve[0] - curve[1]);
                }
                if (i < n - 2)
                {
                    p3 = curve[i + 2];
                }
                else
                {
                    if (closed) p3 = curve[0];
                    else p3 = curve[n - 1] + (curve[n - 1] - curve[n - 2]);
                }
                preStep = ResampleCatmullRomSegment(p0, p1, p2, p3, preStep, stepDistance, result);
            }
            if (preStep > 0.0f)
                result.Add(curve[curve.Length - 1]);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResampleLinearSegment(float3 p1, float3 p2, float preStep, float stepDistance, NativeList<float3> dest)
        {
            float3 dp = p2 - p1;
            float l = math.length(dp);
            if (!(l > 0.0f))
                return preStep;
            float t = preStep;
            while (t <= l)
            {
                dest.Add(p1 + dp * (t / l));
                t += stepDistance;
            }
            return t - l;
        }

        public static NativeList<float3> ResampleLinear(NativeArray<float3> curve, float stepDistance, Allocator allocator)
        {
            int n = curve.Length;
            var result = new NativeList<float3>((n + 1) * 2, allocator);
            if (n == 0)
                return result;
            if (n == 1)
            {
                result.Add(curve[0]);
                return result;
            }
            float preStep = 0;
            for (int i = 0; i < n - 1; i++)
            {
                float3 p1 = curve[i];
                float3 p2 = curve[i + 1];
                preStep = ResampleLinearSegment(p1, p2, preStep, stepDistance, result);
            }
            if (preStep > 0.0f)
                result.Add(curve[curve.Length - 1]);
            return result;
        }

        public static void SmoothCurve(NativeArray<float3> curve)
        {
            int n = curve.Length;
            for (int i = 1; i < n - 1; i++)
                curve[i] = (curve[i - 1] + curve[i] + curve[i + 1]) * (1.0f / 3.0f);
        }

        public static void FillExtrudedLineCircle(NativeArray<LitVertex> vertices, NativeArray<ushort> indices, float radius,
            float taperLen, float uLen, NativeArray<float3> centerline, int nSegments, bool closed)
        {
            NativeArray<float2> profile = new NativeArray<float2>(nSegments, Allocator.TempJob);
            NativeArray<float> taper = new NativeArray<float>(nSegments, Allocator.TempJob);
            for (int i = 0; i < nSegments; i++)
            {
                float f = i / (float)(nSegments - 1);
                float s = math.sin(math.acos(1.0f - f));
                Assert.IsTrue(s >= 0.0f && s <= 1.0f);
                taper[i] = s;
                f *= math.PI * 2.0f;
                profile[i] = new float2(math.sin(f), math.cos(f));
            }
            FillExtrudedLine(vertices, indices, radius, taperLen, uLen, centerline, profile, taper, closed);
            profile.Dispose();
            taper.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleNativeArrayUniformLinear(NativeArray<float> arr, float t)
        {
            Assert.IsTrue(t >= 0.0f && t <= 1.0f);
            Assert.IsTrue(arr.Length >= 2);
            float fidx = (int)(t * (arr.Length - 1));
            int iidx = (int)fidx;
            if (iidx == arr.Length - 1)
                return arr[iidx];
            float f = math.lerp(arr[iidx], arr[iidx + 1], fidx - iidx);
            Assert.IsTrue(f >= 0.0f && f <= 1.0f);
            return f;
        }

        public static void FillPlanarIndices(int tessU, int tessV, NativeArray<ushort> indices)
        {
            int o = 0;
            for (int aOuter = 0; aOuter < tessV - 1; aOuter++)
            {
                for (int aInner = 0; aInner < tessU - 1; aInner++)
                {
                    int iBase = aInner + aOuter * tessU;
                    indices[o++] = (ushort)(iBase + tessU + 1);
                    indices[o++] = (ushort)(iBase + 1);
                    indices[o++] = (ushort)iBase;
                    indices[o++] = (ushort)(iBase + tessU);
                    indices[o++] = (ushort)(iBase + tessU + 1);
                    indices[o++] = (ushort)iBase;
                }
            }
        }

        public static void FillExtrudedLine(NativeArray<LitVertex> vertices, NativeArray<ushort> indices, float radius,
            float taperLen, float uLen, NativeArray<float3> centerline, NativeArray<float2> profile, NativeArray<float> taper, bool closed)
        {
            int nVertices = centerline.Length;
            int nSegments = profile.Length;
            Assert.IsTrue(!closed || math.all(centerline[0] == centerline[nVertices - 1]));
            NativeArray<float3> frameNormals = new NativeArray<float3>(nVertices, Allocator.TempJob);
            NativeArray<float3> frameTangents = new NativeArray<float3>(nVertices, Allocator.TempJob);
            //ParallelTransportFrame(centerline, frameNormals, frameTangents, closed);
            RotationMinimizingFrame(centerline, frameNormals, frameTangents, closed);

            Assert.IsTrue(nVertices >= 3);
            // meassure
            float totalDistance = 0;
            for (int i = 0; i < nVertices - 1; i++)
            {
                float d = math.length(centerline[i + 1] - centerline[i]);
                Assert.IsTrue(d > 0.0f);
                totalDistance += d;
            }
            // vertices
            float distance = 0;
            int o = 0;
            float uScale = 1.0f / uLen;
            for (int i = 0; i < nVertices; i++)
            {
                float3 center = centerline[i];
                float3 nextcenter;
                if (i == nVertices - 1) nextcenter = center + (center - centerline[i - 1]);
                else nextcenter = centerline[i + 1];
                // frame
                float3 forward, up, left;
                forward = frameTangents[i];
                up = frameNormals[i];
                left = math.cross(forward, up);
                float r = radius;
                // taper in/out
                if (distance < taperLen)
                    r *= SampleNativeArrayUniformLinear(taper, distance / taperLen);
                else if (distance > totalDistance - taperLen)
                    r *= SampleNativeArrayUniformLinear(taper, (totalDistance - distance) / taperLen);
                // circle
                for (int j = 0; j < nSegments; j++)
                {
                    float jF = j / (float)(nSegments - 1);
                    float3 n = up * profile[j].x + left * profile[j].y;
                    vertices[o] = new LitVertex
                    {
                        Position = center + n * r,
                        Normal = n,
                        TexCoord0 = new float2(distance * uScale, jF),
                        Tangent = forward,
                        Albedo_Opacity = new float4(1),
                        Metal_Smoothness = new float2(1)
                    };
                    o++;
                }
                distance += math.length(nextcenter - center);
            }
            // indices
            FillPlanarIndices(nSegments, nVertices, indices);
            frameTangents.Dispose();
            frameNormals.Dispose();
        }

        private static float3 InitialNormal(float3 tangent)
        {
            int absMinAxis;
            if (math.abs(tangent.y) < math.abs(tangent.x))
            {
                if (math.abs(tangent.y) < math.abs(tangent.z)) absMinAxis = 1;
                else absMinAxis = 2;
            }
            else
            {
                if (math.abs(tangent.z) < math.abs(tangent.x)) absMinAxis = 2;
                else absMinAxis = 0;
            }
            float3 n = new float3(0);
            n[absMinAxis] = 1.0f;
            float3 bt = math.cross(n, tangent);
            return math.normalize(math.cross(bt, tangent));
        }

        private static void InitTangents(NativeArray<float3> pos, NativeArray<float3> normals, NativeArray<float3> tangents, bool closed)
        {
            Assert.IsTrue(pos.Length == normals.Length && pos.Length == tangents.Length);
            Assert.IsTrue(pos.Length >= 2);
            int n = pos.Length;
            for (int i = 0; i < n - 1; i++)
                tangents[i] = math.normalize(pos[i + 1] - pos[i]);
            if (closed) tangents[n - 1] = tangents[0];
            else tangents[n - 1] = tangents[n - 2];
        }

        private static void PostRotateToCloseFrame(NativeArray<float3> normals, NativeArray<float3> tangents)
        {
            Assert.IsTrue(normals.Length >= 2);
            Assert.IsTrue(normals.Length == tangents.Length);
            int n = normals.Length;
            float theta = math.acos(math.dot(normals[0], normals[n - 1])) / (float)(n - 1);
            if (math.dot(tangents[0], math.cross(normals[0], normals[n - 1])) > 0.0f)
                theta = -theta;
            for (int i = 0; i < n - 1; i++)
            {
                float3x3 rot = float3x3.AxisAngle(tangents[i], theta * (float)i);
                normals[i] = math.mul(rot, normals[i]);
            }
            normals[n - 1] = normals[0];
        }

        // writes normals and tangents
        // based on https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf
        private static void RotationMinimizingFrame(NativeArray<float3> pos, NativeArray<float3> normals, NativeArray<float3> tangents, bool closed)
        {
            InitTangents(pos, normals, tangents, closed);
            normals[0] = InitialNormal(tangents[0]);
            int n = pos.Length;
            for (int i = 0; i < n - 1; i++)
            {
                float3 v1 = pos[i + 1] - pos[i];
                float c1 = math.dot(v1, v1);
                float3 rLi = normals[i] - (2.0f / c1) * math.dot(v1, normals[i]) * v1;
                float3 tLi = tangents[i] - (2.0f / c1) * math.dot(v1, tangents[i]) * v1;
                float3 v2 = tangents[i + 1] - tLi;
                float c2 = math.dot(v2, v2);
                normals[i + 1] = math.normalize(rLi - (2.0f / c2) * math.dot(v2, rLi) * v2);
            }
            if (closed)
                PostRotateToCloseFrame(normals, tangents);
        }

        // writes normals and tangents
        // based on https://janakiev.com/blog/framing-parametric-curves/
        private static void ParallelTransportFrame(NativeArray<float3> pos, NativeArray<float3> normals, NativeArray<float3> tangents, bool closed)
        {
            InitTangents(pos, normals, tangents, closed);
            int n = pos.Length;
            normals[0] = InitialNormal(tangents[0]);
            for (int i = 0; i < n - 1; i++)
            {
                float3 b = math.cross(tangents[i], tangents[i + 1]);
                if (math.lengthsq(b) < 0.0001f)
                {
                    normals[i + 1] = normals[i];
                }
                else
                {
                    b = math.normalize(b);
                    float phi = math.acos(math.dot(tangents[i], tangents[i + 1]));
                    float3x3 rot = float3x3.AxisAngle(b, phi);
                    normals[i + 1] = math.mul(rot, normals[i]);
                }
            }
            if (closed)
                PostRotateToCloseFrame(normals, tangents);
        }

        public static int ExtrudedLineMeshRequiredVertices(int nCenterLineVertices, int nSegments)
        {
            return nCenterLineVertices * nSegments;
        }

        public static int ExtrudedLineMeshRequiredIndices(int nCenterLineVertices, int nSegments)
        {
            return (nCenterLineVertices - 1) * (nSegments - 1) * 6;
        }

        public static void CreateExtrudedLineMesh(float radius, float taperLen, float uLen, int nSegments, NativeArray<float3> centerline, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            int nVertices = centerline.Length;
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LitMeshData>();
            var vertices = builder.Allocate(ref root.Vertices, ExtrudedLineMeshRequiredVertices(nVertices, nSegments));
            var indices = builder.Allocate(ref root.Indices, ExtrudedLineMeshRequiredIndices(nVertices, nSegments));
            FillExtrudedLineCircle(vertices.AsNativeArray(), indices.AsNativeArray(), radius, taperLen, uLen, centerline, nSegments, false);
            mb.Bounds = ComputeBounds(vertices.AsNativeArray());
            lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        static public void CreateSuperTorusKnotMesh(float innerR, int innerN, float outerR, int outerN, int p, int q, float innerE, float outerE, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            Assert.IsTrue(innerN * outerN <= ushort.MaxValue);
            Assert.IsTrue(outerR >= innerR * 2.0f);
            Assert.IsTrue(p > 0 && q >= 0);
            Assert.IsTrue(innerE > 0 && outerE > 0);
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LitMeshData>();
            // in x/y plane
            var vertices = builder.Allocate(ref root.Vertices, innerN * outerN);
            var indices = builder.Allocate(ref root.Indices, (innerN - 1) * (outerN - 1) * 6);
            FillSuperTorusKnot(vertices.AsNativeArray(), indices.AsNativeArray(), innerR, innerN, outerR, outerN, p, q, innerE, outerE);
            mb.Bounds = ComputeBounds(vertices.AsNativeArray());
            lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        static public void CreateDonutMesh(float innerR, int innerN, float outerR, int outerN, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            CreateSuperTorusKnotMesh(innerR, innerN, outerR, outerN, 1, 0, 1, 1, out mb, out lmrd);
        }

        static public void CreateSuperEllipsoidMesh(float3 size, float r, float t, int tessU, int tessV, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            Assert.IsTrue(tessU * tessV <= ushort.MaxValue);
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LitMeshData>();
            var vertices = builder.Allocate(ref root.Vertices, tessU * tessV);
            var indices = builder.Allocate(ref root.Indices, (tessU - 1) * (tessV - 1) * 6);
            CreateSuperEllipsoid(size, vertices.AsNativeArray(), indices.AsNativeArray(), r, t, tessU, tessV, out mb.Bounds);
            lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        // based on https://en.wikipedia.org/wiki/Superellipsoid
        // (parameters e = 2/r, n = 2/t)
        // some shapes:
        // sphere:   e = 1,    n = 1
        // cube:     e = 0.01, n = 0.01
        // cylinder: e = 1,    n = 0.01
        // star:     e = 4,    n = 4
        // spindle:  e = 1,    n = 2
        static public void CreateSuperEllipsoid(float3 size, NativeArray<LitVertex> vertices, NativeArray<ushort> indices, float e, float n, int tessU, int tessV, out AABB bb)
        {
            Assert.IsTrue(e > 0.0f && n > 0.0f);
            Assert.IsTrue(tessU >= 4 && tessV >= 4);
            Assert.IsTrue(math.cmin(size) > 0.0f);
            int o = 0;
            for (int v = 0; v < tessV; v++)
            {
                float fv = (v + .5f) / tessV; // nasty trick: round v, exclude poles
                float rfv = (fv - .5f) * math.PI;
                for (int u = 0; u < tessU; u++)
                {
                    float fu = 1.0f - u / (float)(tessU - 1);
                    float rfu = (fu * 2.0f - 1.0f) * math.PI;
                    float3 pos = new float3(
                        ECos(rfv, n) * ECos(rfu, e),
                        ECos(rfv, n) * ESin(rfu, e),
                        ESin(rfv, n));
                    Assert.IsTrue(math.cmax(math.abs(pos)) <= 1.0f);
                    pos *= size;
                    vertices[o++] = new LitVertex
                    {
                        Position = pos,
                        TexCoord0 = new float2(fu, fv),
                        Albedo_Opacity = new float4(1),
                        Metal_Smoothness = new float2(1)
                    };
                }
            }
            FillPlanarIndices(tessU, tessV, indices);
            ComputeNormals(vertices, indices);
            ComputeTangentAndBinormal(vertices, indices);
            // now weld pole position together, but normals and tangents and so on will still
            // have useful values
            float3 pos0 = new float3(0, 0, -size.z);
            float3 pos1 = new float3(0, 0,  size.z);
            int ko = tessU * tessV - 1;
            unsafe {
                LitVertex *verts = (LitVertex *)vertices.GetUnsafePtr();
                for (int u = 0; u < tessU; u++)
                {
                    verts[u].Position = pos0;
                    verts[ko - u].Position = pos1;
                }
            }
            bb.Center = new float3(0);
            bb.Extents = size;
        }

        static public void FillBoxMesh(float3 size, NativeArray<LitVertex> vertices, NativeArray<ushort> indices, out AABB bb)
        {
            int destI = 0, destV = 0;
            AddBoxFace(vertices, indices, 0, -1, size, ref destI, ref destV, 1.0f);
            AddBoxFace(vertices, indices, 0, 1, size, ref destI, ref destV, 1.0f);
            AddBoxFace(vertices, indices, 1, -1, size, ref destI, ref destV, 1.0f);
            AddBoxFace(vertices, indices, 1, 1, size, ref destI, ref destV, 1.0f);
            AddBoxFace(vertices, indices, 2, -1, size, ref destI, ref destV, 1.0f);
            AddBoxFace(vertices, indices, 2, 1, size, ref destI, ref destV, 1.0f);

            ComputeNormals(vertices, indices);
            ComputeTangentAndBinormal(vertices, indices);
            SetAlbedoColor(vertices, new float4(1));
            SetMetalSmoothness(vertices, new float2(1));

            bb.Center = new float3(0);
            bb.Extents = size;
        }

        static public NativeArray<T> AsNativeArray<T>(this BlobBuilderArray<T> bba) where T : struct
        {
            unsafe {
                var r = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(bba.GetUnsafePtr(), bba.Length, Allocator.Invalid);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref r, AtomicSafetyHandle.GetTempMemoryHandle());
                #endif
                return r;
            }
        }

        static public NativeArray<T> AsNativeArray<T>(ref BlobArray<T> ba) where T : struct
        {
            unsafe {
                var r = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ba.GetUnsafePtr(), ba.Length, Allocator.Invalid);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref r, AtomicSafetyHandle.GetTempMemoryHandle());
                #endif
                return r;
            }
        }

        static public void CreateBoxMesh(float3 size, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LitMeshData>();
            var vertices = builder.Allocate(ref root.Vertices, 24);
            var indices = builder.Allocate(ref root.Indices, 36);
            FillBoxMesh(size, vertices.AsNativeArray(), indices.AsNativeArray(), out mb.Bounds);
            lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        static public void FillPlaneMesh(NativeArray<SimpleVertex> destVertices, NativeArray<ushort> destIndices, float3 org, float3 du, float3 dv, out AABB bb)
        {
            destVertices[0] = new SimpleVertex { Position = org, Color = new float4(1, 1, 1, 1), TexCoord0 = new float2(0, 0) };
            destVertices[1] = new SimpleVertex { Position = org + du, Color = new float4(1, 1, 1, 1), TexCoord0 = new float2(1, 0) };
            destVertices[2] = new SimpleVertex { Position = org + du + dv, Color = new float4(1, 1, 1, 1), TexCoord0 = new float2(1, 1) };
            destVertices[3] = new SimpleVertex { Position = org + dv, Color = new float4(1, 1, 1, 1), TexCoord0 = new float2(0, 1) };
            destIndices[0] = 0; destIndices[1] = 1; destIndices[2] = 2;
            destIndices[3] = 2; destIndices[4] = 3; destIndices[5] = 0;
            bb = ComputeBounds(destVertices);
        }

        static public void CreatePlane(float3 org, float3 du, float3 dv, out MeshBounds mb, out SimpleMeshRenderData smrd)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SimpleMeshData>();
            var vertices = builder.Allocate(ref root.Vertices, 4);
            var indices = builder.Allocate(ref root.Indices, 6);
            FillPlaneMesh(vertices.AsNativeArray(), indices.AsNativeArray(), org, du, dv, out mb.Bounds);
            smrd.Mesh = builder.CreateBlobAssetReference<SimpleMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        static public void FillPlaneMeshLit(NativeArray<LitVertex> destVertices, NativeArray<ushort> destIndices, float3 org, float3 du, float3 dv, out AABB bb)
        {
            destVertices[0] = new LitVertex { Position = org, TexCoord0 = new float2(0, 0) };
            destVertices[1] = new LitVertex { Position = org + du, TexCoord0 = new float2(1, 0) };
            destVertices[2] = new LitVertex { Position = org + du + dv, TexCoord0 = new float2(1, 1) };
            destVertices[3] = new LitVertex { Position = org + dv, TexCoord0 = new float2(0, 1) };
            destIndices[0] = 0; destIndices[1] = 2; destIndices[2] = 1;
            destIndices[3] = 2; destIndices[4] = 0; destIndices[5] = 3;
            bb = ComputeBounds(destVertices);
        }

        static public void CreatePlaneLit(float3 org, float3 du, float3 dv, out MeshBounds mb, out LitMeshRenderData lmrd)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LitMeshData>();
            var vertices = builder.Allocate(ref root.Vertices, 4).AsNativeArray();
            var indices = builder.Allocate(ref root.Indices, 6).AsNativeArray();
            FillPlaneMeshLit(vertices, indices, org, du, dv, out mb.Bounds);
            ComputeNormals(vertices, indices);
            ComputeTangentAndBinormal(vertices, indices);
            SetAlbedoColor(vertices, new float4(1));
            SetMetalSmoothness(vertices, new float2(1));
            lmrd.Mesh = builder.CreateBlobAssetReference<LitMeshData>(Allocator.Persistent);
            builder.Dispose();
        }

        public static void SetAlbedoColor(NativeArray<LitVertex> dest, float4 albedo_opacity)
        {
            unsafe {
                int n = dest.Length;
                LitVertex *vp = (LitVertex *)dest.GetUnsafePtr();
                for (int i = 0; i < n; i++)
                    vp[i].Albedo_Opacity = albedo_opacity;
            }
        }

        public static void SetMetalSmoothness(NativeArray<LitVertex> dest, float2 metal_smoothness)
        {
            unsafe {
                int n = dest.Length;
                LitVertex *vp = (LitVertex *)dest.GetUnsafePtr();
                for (int i = 0; i < n; i++)
                    vp[i].Metal_Smoothness = metal_smoothness;
            }
        }
    }
}
