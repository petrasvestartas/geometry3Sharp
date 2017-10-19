﻿using System;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    abstract public class MeshGenerator
    {
        public VectorArray3d vertices;
        public VectorArray2f uv;
        public VectorArray3f normals;
        public IndexArray3i triangles;
        public int[] groups;

        public bool WantUVs = true;
        public bool WantNormals = true;
        public bool WantGroups = true;

        // "normal" meshes are counter-clockwise. Unity is CW though...
        public bool Clockwise = false;



        abstract public void Generate();


        public virtual void MakeMesh(SimpleMesh m)
        {
            m.AppendVertices(vertices, (WantNormals) ? normals : null, null, (WantUVs) ? uv : null);
            m.AppendTriangles(triangles);
        }
        public virtual SimpleMesh MakeSimpleMesh()
        {
            SimpleMesh m = new SimpleMesh();
            MakeMesh(m);
            return m;
        }

        public virtual void MakeMesh(DMesh3 m)
        {
            int nV = vertices.Count;
            for (int i = 0; i < nV; ++i) {
				NewVertexInfo ni = new NewVertexInfo() { v = vertices[i] };
				if ( WantNormals ) {
					ni.bHaveN = true; 
					ni.n = normals[i];
				}
				if ( WantUVs ) {
					ni.bHaveUV = true;
					ni.uv = uv[i];
				}
                int vID = m.AppendVertex(ni);
                Util.gDevAssert(vID == i);
            }
            int nT = triangles.Count;
            if (WantGroups && groups != null && groups.Length == nT) {
                for (int i = 0; i < nT; ++i)
                    m.AppendTriangle(triangles[i], groups[i]);
            } else {
                for (int i = 0; i < nT; ++i)
                    m.AppendTriangle(triangles[i]);
            }
        }
        public virtual DMesh3 MakeDMesh()
        {
            DMesh3 m = new DMesh3();
            MakeMesh(m);
            return m;
        }




        public struct CircularSection
        {
            public float Radius;
            public float SectionY;
            public CircularSection(float r, float y)
            {
                Radius = r;
                SectionY = y;
            }
        }


        protected void duplicate_vertex_span(int nStart, int nCount)
        {
            for (int i = 0; i < nCount; ++i) {
                vertices[(nStart + nCount) + i] = vertices[nStart + i];
                normals[(nStart + nCount) + i] = normals[nStart + i];
                uv[(nStart + nCount) + i] = uv[nStart + i];
            }
        }


        protected void append_disc(int Slices, int nCenterV, int nRingStart, bool bClosed, bool bCycle, ref int tri_counter, int groupid = -1)
        {
            int nLast = nRingStart + Slices;
            for (int k = nRingStart; k < nLast - 1; ++k) {
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, k, nCenterV, k + 1, bCycle);
            }
            if (bClosed) {     // close disc if we went all the way
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, nLast - 1, nCenterV, nRingStart, bCycle);
            }
        }

        // assumes order would be [v0,v1,v2,v3], ccw
        protected void append_rectangle(int v0, int v1, int v2, int v3, bool bCycle, ref int tri_counter, int groupid = -1)
        {
            if ( groupid >= 0 )
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, v0, v1, v2, bCycle);
            if ( groupid >= 0 )
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, v0, v2, v3, bCycle);
        }


        // append "disc" verts/tris between vEnd1 and vEnd2
        protected void append_2d_disc_segment(int iCenter, int iEnd1, int iEnd2, int nSteps,bool bCycle, ref int vtx_counter, ref int tri_counter, int groupid = -1, double force_r = 0)
        {
            Vector3d c = vertices[iCenter];
            Vector3d e0 = vertices[iEnd1];
            Vector3d e1 = vertices[iEnd2];
            Vector3d v0 = (e0 - c);
            double r0 = v0.Normalize();
            if (force_r > 0)
                r0 = force_r;
            double tStart = Math.Atan2(v0.z, v0.x);
            Vector3d v1 = (e1 - c);
            double r1 = v1.Normalize();
            if (force_r > 0)
                r1 = force_r;
            double tEnd = Math.Atan2(v1.z, v1.x);

            // fix angles to handle sign. **THIS ONLY WORKS IF WE ARE GOING CCW!!**
            if (tStart < 0)
                tStart += MathUtil.TwoPI;
            if (tEnd < 0)
                tEnd += MathUtil.TwoPI;
            if (tEnd < tStart)
                tEnd += MathUtil.TwoPI;

            int iPrev = iEnd1;
            for ( int i = 0; i < nSteps; ++i ) {
                double t = (double)(i+1) / (double)(nSteps + 1);
                double angle = (1 - t) * tStart + (t) * tEnd;
                Vector3d pos = c + new Vector3d(r0 * Math.Cos(angle), 0, r1 * Math.Sin(angle));
                vertices.Set(vtx_counter, pos.x, pos.y, pos.z);
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, iCenter, iPrev, vtx_counter, bCycle);
                iPrev = vtx_counter++;
            }
            if (groupid >= 0)
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, iCenter, iPrev, iEnd2, bCycle);
        }

        protected Vector3f estimate_normal(int v0, int v1, int v2)
        {
            Vector3d a = vertices[v0];
            Vector3d b = vertices[v1];
            Vector3d c = vertices[v2];
            Vector3d e1 = (b - a).Normalized;
            Vector3d e2 = (c - a).Normalized;
            return new Vector3f(e1.Cross(e2));
        }


#if G3_USING_UNITY
        // generate unity mesh. 
        // [TODO] The left/right flip here may not work...

        static Vector3[] ToUnityVector3(VectorArray3f a, bool bFlipLR = false) {
            Vector3[] v = new Vector3[a.Count];
            float fZSign = (bFlipLR) ? -1 : 1;
            for (int i = 0; i < a.Count; ++i) {
                v[i].x = a.array[3 * i];
                v[i].y = a.array[3 * i + 1];
                v[i].z = fZSign * a.array[3 * i + 2];
            }
            return v;
        }
        static Vector3[] ToUnityVector3(VectorArray3d a, bool bFlipLR = false) {
            Vector3[] v = new Vector3[a.Count];
            float fZSign = (bFlipLR) ? -1 : 1;
            for (int i = 0; i < a.Count; ++i) {
                v[i].x = (float)a.array[3 * i];
                v[i].y = (float)a.array[3 * i + 1];
                v[i].z = fZSign * (float)a.array[3 * i + 2];
            }
            return v;
        }
        static Vector2[] ToUnityVector2(VectorArray2f a) {
            Vector2[] v = new Vector2[a.Count];
            for (int i = 0; i < a.Count; ++i) {
                v[i].x = (float)a.array[2 * i];
                v[i].y = (float)a.array[2 * i + 1];
            }
            return v;
        }

        /// <summary>
        /// copy generated mesh data into a Unity Mesh object
        /// </summary>
        public void MakeMesh(Mesh m, bool bRecalcNormals = false, bool bFlipLR = false)
        {
            m.vertices = ToUnityVector3(vertices, bFlipLR);
            if (uv != null && WantUVs)
                m.uv = ToUnityVector2(uv);
            if (normals != null && WantNormals)
                m.normals = ToUnityVector3(normals, bFlipLR);
            m.triangles = triangles.array;
            if (bRecalcNormals)
                m.RecalculateNormals();
        }
#endif
    }








}
