using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityToolKit
{
    public class LineMeshGenerator
    {
        [Serializable]
        public struct MyVector3
        {
            public Vector3 pos;
            public Vector3 tangent;

            public Vector3 forward;

            public Vector3 normal;

            public Vector3 leftNormal;
            public Vector3 rightNormal;


            public bool isPosDirty;

            public float leftWidth;
            public float rightWidth;
            public float thick;

            public Vector3 leftPos;
            public Vector3 rightPos;
            public Vector3 centerThickPos;
            public Vector3 leftThickPos;
            public Vector3 rightThickPos;
            public bool isGenDirty;

            public float lengthInAll;
            public float previousLength;


            public void UpdateNormalAndTangent(MyVector3 previous, MyVector3 next)
            {
                var pp = pos - previous.pos;
                var p1 = pp.normalized;
                var p2 = (next.pos - pos).normalized;
                forward = p2;
                tangent = (p1 + p2).normalized;
                if (tangent == Vector3.zero)
                {
                    if (p1 != Vector3.zero)
                    {
                        // back point use previous tangent
                        tangent = previous.tangent;
                    }
                    else
                        Debug.LogError("---> tangent is ZERO \n" + previous.pos + " -> " + this.pos + " -> " +
                                       next.pos);
                }


                normal = Vector3.Cross(new Vector3(0, 0, -1), tangent).normalized;
                if (normal == Vector3.zero)
                {
                    Debug.LogError("---> normal is ZERO\n" + previous.pos + " -> " + this.pos + " -> " + next.pos);
                }

                leftNormal = normal;
                rightNormal = -normal;
                previousLength = pp.magnitude;


                // Debug.Log(tangent + " ---> " + normal+"\n"+ this.ToString());
                lengthInAll = previous.lengthInAll + pp.magnitude;
                isPosDirty = false;
            }

            public void GenerateOtherPositions(MyVector3 previous)
            {
                leftPos = pos + leftNormal * leftWidth;

                if (previousLength < leftWidth + previous.leftWidth && Vector3.Angle(leftNormal, forward) < 90)
                {
                    // Debug.LogError("--->left");
                    leftPos = (leftPos + previous.leftPos) * 0.5f;
                    previous.leftPos = leftPos;
                }

                rightPos = pos + rightNormal * rightWidth;
                if (previousLength < rightWidth + previous.rightWidth && Vector3.Angle(rightNormal, forward) < 90)
                {
                    // Debug.LogError("--->right");
                    rightPos = (rightPos + previous.rightPos) * 0.5f;
                    previous.rightPos = rightPos;
                }


                leftThickPos = leftPos + Vector3.forward * thick;
                rightThickPos = rightPos + Vector3.forward * thick;

                isGenDirty = false;
            }

            public override string ToString()
            {
                return JsonUtility.ToJson(this);
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
            set { mLeftWidth = value; }
        }

        public float rightWidth
        {
            get { return mRightWidth; }
            set { mRightWidth = value; }
        }

        private bool isDirty
        {
            get { return _isDirty; }
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
                if (CheckIndex(index, true))
                    return positions[index].pos;
                return Vector3.zero;
            }
            set
            {
                if (CheckIndex(index, true))
                {
                    var p = positions[index];
                    p.pos = value;
                    p.isPosDirty = true;
                    positions[index] = p;
                    isDirty = true;
                }
            }
        }

        public Vector3 GetProgressPosition(float percent)
        {
            //TODO:
            return Vector3.zero;
        }

        public void Add(Vector3 pos)
        {
            if (Count > 0)
            {
                var myVector3 = positions[Count - 1];
                if (Vector3.Distance(myVector3.pos, pos) <= .35f*(leftWdith + rightWidth))
                {
                    //Debug.Log("Same pos as previous one");
                    return;
                }

                myVector3.isPosDirty = true;
                positions[Count - 1] = myVector3;
            }

            var p = new MyVector3()
            {
                pos = pos,
                leftWidth = leftWdith,
                rightWidth = rightWidth,
                thick = thick,
                isGenDirty = true,
                isPosDirty = true,
            };

            positions.Add(p);


            isDirty = true;
        }

        public void AddWithWidth(Vector3 pos, float leftWidth, float rightWidth)
        {
            UpdateWidth(leftWidth, rightWidth, false);
            Add(pos);
        }

        public void RemoveAt(int index)
        {
            if (CheckIndex(index))
                positions.RemoveAt(index);
            if (CheckIndex(index - 1))
            {
                SetGenDirty(index - 1, true);
            }

            if (CheckIndex(index - 2))
            {
                SetGenDirty(index - 2, true);
            }

            if (CheckIndex(index))
            {
                SetGenDirty(index, true);
            }

            if (CheckIndex(index + 1))
            {
                SetGenDirty(index + 1, true);
            }
        }

        public void Clear()
        {
            positions.Clear();
            isDirty = true;
            mesh.Clear();
        }

        public void UpdateWidth(float leftWidth, float rightWidth, bool shouldGen = true)
        {
            this.leftWdith = leftWidth;
            this.rightWidth = rightWidth;
            if (shouldGen) Generate();
        }

        public int Count
        {
            get { return positions.Count; }
        }

        public int VertexCount
        {
            get { return positions.Count * 6; }
        }

        public int TriangleCount
        {
            get { return (Count - 1) * 12; }
        }

        public Mesh mesh;

        public LineMeshGenerator(float left, float right, float thick) : this()
        {
            mLeftWidth = left;
            mRightWidth = right;
            this.mThick = thick;
        }

        void SetGenDirty(int inx, bool isGenDirty)
        {
            var p = positions[inx];
            p.isGenDirty = isGenDirty;
            positions[inx] = p;
        }

        bool CheckIndex(int index, bool logError = false)
        {
            bool isValid = index >= 0 && index < Count;
            if (logError && !isValid)
                Debug.LogError("Index out range: " + index + "  from  " + Count);
            return isValid;
        }


        void SimplePositions()
        {
            if (Count < 2)
            {
                return;
            }

            var p = positions[0];
            for (int i = 1; i < Count; i++)
            {
                if (p.pos == positions[i].pos)
                {
                    RemoveAt(i);
                }
            }
        }

        void Generate()
        {
            SimplePositions();
            bool isMeshDirty = false;

            if (Count < 2)
            {
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                var p = positions[i];
                if (p.isPosDirty)
                {
                    isMeshDirty = true;
                    if (i > 0 && i < Count - 1)
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

                        if (i < Count - 2)
                        {
                            next.UpdateNormalAndTangent(p, positions[i + 2]);
                            next.isGenDirty = true;
                            positions[i + 1] = next;
                        }
                    }
                    else if (i == 0)
                    {
                        p.UpdateNormalAndTangent(p, i + 1 < Count ? positions[i + 1] : p);
                    }
                    else
                    {
                        p.UpdateNormalAndTangent(i - 1 >= 0 ? positions[i - 1] : p, p);
                    }

                    p.isGenDirty = true;
                    positions[i] = p;
                }
            }

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length != VertexCount)
                vertices = new Vector3[VertexCount];

            var triangles = mesh.triangles;
            if (Count > 0)
            {
                if (triangles == null || triangles.Length != TriangleCount)
                    triangles = new int[TriangleCount];
            }
            else
            {
                mesh.triangles = new int[0];
                triangles = new int[0];
            }

            var uv = mesh.uv;
            if (uv == null || uv.Length != VertexCount)
                uv = new Vector2[VertexCount];

            for (int i = 0; i < Count; i++)
            {
                var p = positions[i];
                if (p.isGenDirty || isDirty)
                {
                    isMeshDirty = true;
                    p.GenerateOtherPositions(i == 0 ? p : positions[i - 1]);
                    positions[i] = p;
                }

                vertices[6 * i] = positions[i].leftPos;
                vertices[6 * i + 1] = positions[i].rightPos;
                vertices[6 * i + 2] = positions[i].pos;
                vertices[6 * i + 3] = positions[i].leftThickPos;
                vertices[6 * i + 4] = positions[i].rightThickPos;
                vertices[6 * i + 5] = positions[i].centerThickPos;
                if (i < Count - 1)
                {
                    triangles[12 * i + 0] = 6 * i;
                    triangles[12 * i + 1] = 6 * i + 2;
                    triangles[12 * i + 2] = 6 * (i + 1) + 2;
                    triangles[12 * i + 3] = 6 * (i + 1) + 2;
                    triangles[12 * i + 4] = 6 * (i + 1);
                    triangles[12 * i + 5] = 6 * i;
                    triangles[12 * i + 6] = 6 * i + 2;
                    triangles[12 * i + 7] = 6 * i + 1;
                    triangles[12 * i + 8] = 6 * (i + 1) + 1;
                    triangles[12 * i + 9] = 6 * (i + 1) + 1;
                    triangles[12 * i + 10] = 6 * (i + 1) + 2;
                    triangles[12 * i + 11] = 6 * i + 2;
                }

                uv[6 * i + 0] = new Vector2(0, p.lengthInAll / 64);
                uv[6 * i + 1] = new Vector2(1, p.lengthInAll / 64);
                uv[6 * i + 2] = new Vector2(0.5f, p.lengthInAll / 64);
                uv[6 * i + 3] = new Vector2(0, 0);
                uv[6 * i + 4] = new Vector2(1, 1);
                uv[6 * i + 5] = new Vector2(0, 0);
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            if (isMeshDirty)
                mesh.UploadMeshData(false);

            isDirty = false;
        }
    }
}