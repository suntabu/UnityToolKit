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
            line.Add(positions[i]);
        }
    }

    private bool isDraw = false;
    private float t;
    void Update()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            line[i] = positions[i];
        }
        
        var w = (Mathf.Sin(t* 4) + 1.25f) * width;
        if (Input.GetMouseButtonDown(0))
        {
            isDraw = true;
            line.Clear();
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
    }
}