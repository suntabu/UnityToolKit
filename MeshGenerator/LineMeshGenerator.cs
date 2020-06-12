using System.Collections.Generic;
using UnityEngine;

namespace UnityToolKit
{
    public class LineMeshGenerator
    {
        public struct MyVector3
        {
            public Vector3 pos;
            public Vector3 tangent;
            public Vector3 normal;
            public bool isPosDirty;

            public Vector3 leftPos;
            public Vector3 rightPos;
            public Vector3 leftThickPos;
            public Vector3 rightThickPos;
            public bool isGenDirty;

            public void UpdateNormalAndTangent(MyVector3 previous, MyVector3 next)
            {
                var p1 = (pos - previous.pos).normalized;
                var p2 = (next.pos - pos).normalized;
                tangent = (p1 + p2).normalized;
                normal = Vector3.Cross(new Vector3(0, 0, -1), tangent).normalized;

                isPosDirty = false;
            }

            public void GenerateOtherPositions(float leftW, float rightW, float thick)
            {
                leftPos = pos + normal * leftW;
                rightPos = pos - normal * rightW;
                leftThickPos = leftPos + Vector3.forward * thick;
                rightThickPos = rightPos + Vector3.forward * thick;

                isGenDirty = false;
            }
        }

        public LineMeshGenerator()
        {
            positions = new List<MyVector3>();
            mesh = new Mesh();
        }

        private List<MyVector3> positions;
        private bool _isDirty;

        private float mLeftWidth, mRightWidth;

        private float mThick;

        public float thick
        {
            get { return mThick; }
            set
            {
                mThick = value;
                isDirty = true;
            }
        }

        public float leftWdith
        {
            get { return mLeftWidth; }
            set
            {
                mLeftWidth = value;
                isDirty = true;
            }
        }

        public float rightWidth
        {
            get { return mRightWidth; }
            set
            {
                mRightWidth = value;
                isDirty = true;
            }
        }

        private bool isDirty
        {
            get => _isDirty;
            set
            {
                _isDirty = value;
                if (value)
                    Generate();
            }
        }

        public Vector3 this[int index]
        {
            get
            {
                CheckIndex(index);
                return positions[index].pos;
            }
            set
            {
                CheckIndex(index);
                var p = positions[index];
                p.pos = value;
                p.isPosDirty = true;
                positions[index] = p;
                isDirty = true;
            }
        }

        public void Add(Vector3 pos)
        {
            var p = new MyVector3()
            {
                pos = pos,
                isGenDirty = true,
                isPosDirty = true,
            };
            if (positions.Count > 0)
            {
                var myVector3 = positions[positions.Count - 1];
                myVector3.isPosDirty = true;
                positions[positions.Count - 1] = myVector3;
            }

            positions.Add(p);


            isDirty = true;
        }

        public void RemoveAt(int index)
        {
            //TODO:
        }

        public void Clear()
        {
            positions.Clear();
            isDirty = true;
        }

        public Mesh mesh;

        public LineMeshGenerator(float left, float right, float thick) : this()
        {
            mLeftWidth = left;
            mRightWidth = right;
            this.mThick = thick;
        }

        void CheckIndex(int index)
        {
            Debug.Assert(index >= 0 && index < positions.Count,
                "Out of range index: " + index + "  Count:" + positions.Count);
        }


        void Generate()
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                if (p.isPosDirty)
                {
                    if (i > 0 && i < positions.Count - 1)
                    {
                        var previous = positions[i - 1];
                        var next = positions[i + 1];
                        p.UpdateNormalAndTangent(previous, next);

                        if (i > 1)
                        {
                            previous.UpdateNormalAndTangent(positions[i - 2], p);
                            previous.isGenDirty = true;
                            positions[i - 1] = previous;
                        }

                        if (i < positions.Count - 2)
                        {
                            next.UpdateNormalAndTangent(p, positions[i + 2]);
                            next.isGenDirty = true;
                            positions[i + 1] = next;
                        }
                    }
                    else if (i == 0)
                    {
                        p.UpdateNormalAndTangent(p, i + 1 < positions.Count ? positions[i + 1] : p);
                    }
                    else
                    {
                        p.UpdateNormalAndTangent(i - 1 > 0 ? positions[i - 1] : p, p);
                    }

                    p.isGenDirty = true;
                    positions[i] = p;
                }
            }

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length != positions.Count * 4)
                vertices = new Vector3[positions.Count * 4];

            var triangles = mesh.triangles;
            if (triangles == null || triangles.Length != (positions.Count - 1) * 6)
                triangles = new int[(positions.Count - 1) * 6];

            var uv = mesh.uv;
            if (uv == null || uv.Length != positions.Count * 4)
                uv = new Vector2[positions.Count * 4];

            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                if (p.isGenDirty || isDirty)
                {
                    p.GenerateOtherPositions(leftWdith, rightWidth, thick);
                    positions[i] = p;
                }

                vertices[4 * i] = positions[i].leftPos;
                vertices[4 * i + 1] = positions[i].rightPos;
                vertices[4 * i + 2] = positions[i].rightThickPos;
                vertices[4 * i + 3] = positions[i].leftThickPos;
                if (i < positions.Count - 1)
                {
                    triangles[6 * i + 0] = 4 * i;
                    triangles[6 * i + 2] = 4 * (i + 1);
                    triangles[6 * i + 1] = 4 * (i + 1) + 1;
                    triangles[6 * i + 3] = 4 * i;
                    triangles[6 * i + 5] = 4 * (i + 1) + 1;
                    triangles[6 * i + 4] = 4 * i + 1;
                }

                uv[4 * i + 0] = new Vector2(0, i);
                uv[4 * i + 1] = new Vector2(1, i);
                uv[4 * i + 2] = new Vector2(1, 1);
                uv[4 * i + 3] = new Vector2(0, 0);
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.UploadMeshData(false);
            
            isDirty = false;
        }
    }
}