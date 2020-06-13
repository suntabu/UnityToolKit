using System.Collections;
using System.Collections.Generic;
using DG.Tweening.Plugins;
using UnityEngine;
using UnityToolKit;

public class LineMeshGeneratorTest : MonoBehaviour
{
    private LineMeshGenerator line;

    public Material mat;

    void Start()
    {
        line = new LineMeshGenerator(10, 10, 10);
        var filter = GetComponent<MeshFilter>();
        filter.mesh = line.mesh;
        GetComponent<MeshRenderer>().material = mat;
        for (int i = 0; i < 100; i++)
        {
            line.Add(new Vector3(i * 10, i * 10));
        }
    }

    private bool isDraw = false;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDraw = true;
            line.Clear();
        }

        if (isDraw && Input.GetMouseButton(0))
        {
            line.Add(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDraw = false;
        }
    }
}