using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelGrid : MonoBehaviour
{
    public Vector3 boundsExtent = new Vector3(10, 10, 10);

    public float voxelSize = 0.5f;

    public int voxelsX, voxelsY, voxelsZ, numberOfVoxels;

    public Vector3 GetVoxelResolution() {
        return new Vector3(voxelsX, voxelsY, voxelsZ);
    }

    public Vector3 GetBoundsExtent() {
        return boundsExtent;
    }

    public float GetVoxelSize() {
        return voxelSize;
    }

   

   // rendering the smoke


   // easing function
   float  Easing(float x) {
        float ease = 0.0f;
    
        if (x < 0.5f) {
            ease = 2*x*x;
        } else {
            ease = 1.0f - (1.0f / (5.0f * (2.0f * x - 0.8f) + 1));
        }
    
        return Mathf.Min(1.0f, ease);
   }




    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
