using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityToolKit;

public class LineMeshGeneratorTest : MonoBehaviour
{
    private LineMeshGenerator line;

    public Material mat;

    [SerializeField] private float mWidth = 0.2f;

    public Vector3[] positions;

    public float width
    {
        get { return mWidth; }
        set
        {
            mWidth = value;

            Debug.Log("--->");
        }
    }

    void Start()
    {
        line = new LineMeshGenerator(width, width, 10);
        var filter = GetComponent<MeshFilter>();
        filter.mesh = line.mesh;
        GetComponent<MeshRenderer>().material = mat;
        for (int i = 0; i < positions.Length; i++)
        {
            // line.Add(positions[i]);
        }
    }

    private bool isDraw = false;
    private float t;
    private GameObject[] gos;

    void Update()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            // line[i] = positions[i];
        }

        var w = (Mathf.Sin(t * 4) + 1.25f) * width;
        if (Input.GetMouseButtonDown(0))
        {
            isDraw = true;
            // line.Clear();
        }

        if (isDraw && Input.GetMouseButton(0))
        {
            // t += Time.deltaTime;
            line.leftWdith = mWidth;
            line.rightWidth = mWidth;
            var p = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            line.AddWithWidth(p, w, w);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDraw = false;
        }

        if (gos != null)
        {
            for (int i = 0; i < gos.Length; i++)
            {
                gos[i].transform.localScale = Mathf.Sin(i * 2f / gos.Length * Mathf.PI + Time.time) * Vector3.one;
            }
        }
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 100, 100), "Click"))
        {
            gos = new GameObject[50];
            for (int i = 0; i < gos.Length; i++)
            {
                var p = line.GetProgressPosition(0.02f * (i + 1));
                gos[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gos[i].transform.SetParent(this.transform);
                gos[i].transform.localPosition = p;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (line != null)
            line.DrawGizmos(this.transform);
    }
}