using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public class VoxelGrid : MonoBehaviour
{
    public bool drawVoxelGridGizmo = true;
    public bool drawBoxGizmo = true;
    
    
    [Header("Grid voxelization")]
    [Space(5)]
    public Vector3 boundsExtent = new Vector3(50, 5, 50);
    public float voxelSize = 0.5f;
    private int voxelsX, voxelsY, voxelsZ, numberOfVoxels;
    
    private ComputeBuffer _voxelsBuffer;
    private ComputeBuffer _staticVoxelsBuffer;
    private ComputeBuffer _smokeVoxelsBuffer;
    private ComputeBuffer _floodFillSmokeBuffer;
    private ComputeBuffer _argsBuffer;
    
    
    private Material _voxelGridVisualization;
    private ComputeShader _voxelizeCompute;
    public bool drawStaticScene = false;
    public bool drawSmoke = true;
    private bool drawVoxelGrid = false;
    private bool _debugAllVoxels = false;
    private bool _debugStaticVoxels = false;
    private bool _debugSmokeVoxels = false;
    
    
    
    private Bounds debugBounds;
    
    [Header("Smoke voxels rendering")]
    [Space(5)]
    public int MaxFillSteps;
    
    public Mesh debugMesh;
    
    
    
    

    public enum SmokeShape {
        Cloud,
        Plume
    }
    
    
    [SerializeField]
    private SmokeShape _selectedSmokeShape;

    private bool _cloudSmoke = true;
    private bool _plumeSmoke = false;
    
    private Vector3 _smokeOrigin;
    private float _radius;
    private float _height;
    private bool _smokeConstantFill = false;
    private bool _smokeIterateFill = false;
    [Range(0.01f, 5.0f)]
    public float smokeGrowthSpeed = 1.0f;
    
    private Vector3 maxRadius;
    private Vector3 maxPlumeHeight;
    
    [Header("Static scene voxelization")]
    [Space(5)]
    public GameObject sceneToVoxelize;
    private ComputeBuffer _vertices, _triangles;
    [Range(0.0f, 2.0f)]
    public float intersectionBias = 1.0f;
    
    
    public ComputeBuffer GetSmokeVoxelBuffer()
    {
        return _smokeVoxelsBuffer;
    }

    public Vector3 GetVoxelResolution()
    {
        return new Vector3(voxelsX, voxelsY, voxelsZ);
    }

    public Vector3 GetBoundsExtent()
    {
        return boundsExtent;
    }
    public float GetVoxelSize() {
        return voxelSize;
    }

    public Vector3 GetSmokeOrigin() {
        return _smokeOrigin;
    }

    public Vector3 GetSmokeRadius() {
        return Vector3.Lerp(Vector3.zero, maxRadius, EasingFunction(_radius));
    }

    public float GetEasing() {
        return EasingFunction(_radius);
    }

   private void OnEnable()
   {
       _radius = 0.0f;
       _height = 0.0f;
       // create a voxel grid
       Vector3 boundsSize = boundsExtent * 2;
       debugBounds = new Bounds(new Vector3(0, boundsExtent.y, 0), boundsSize);
       
       // calculate number of voxels in each direction
       voxelsX = Mathf.CeilToInt(boundsSize.x / voxelSize);
       voxelsY = Mathf.CeilToInt(boundsSize.y / voxelSize);
       voxelsZ = Mathf.CeilToInt(boundsSize.z / voxelSize);

       // get full voxel grid resolution
       numberOfVoxels = voxelsX * voxelsY * voxelsZ;
       // Debug.Log("Number of voxels:" + numberOfVoxels);
       
       
       // shader for visualizing voxels
       _voxelGridVisualization = new Material(Shader.Find("Custom/VisualizeVoxels1"));

       // compute buffer for voxel grid
       _voxelsBuffer = new ComputeBuffer(numberOfVoxels, sizeof(int));
       // compute buffer for representation of static object in the scene
       _staticVoxelsBuffer = new ComputeBuffer(numberOfVoxels, sizeof(int));
       // compute buffer for representation of smoke
       _smokeVoxelsBuffer = new ComputeBuffer(numberOfVoxels, sizeof(int));
       
       _floodFillSmokeBuffer = new ComputeBuffer(numberOfVoxels, sizeof(int));
       
      
       
       // compute shader for voxelization of the scene
       _voxelizeCompute = (ComputeShader)Resources.Load("VoxelGrid");
       

       _voxelizeCompute.SetBuffer(1, "_Voxels", _voxelsBuffer);
       _voxelizeCompute.Dispatch(1, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);
       
       _voxelizeCompute.SetBuffer(0, "_Voxels", _smokeVoxelsBuffer);
       _voxelizeCompute.Dispatch(0, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);
       
       _voxelizeCompute.SetBuffer(0, "_Voxels", _floodFillSmokeBuffer);
       _voxelizeCompute.Dispatch(0, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);

       _voxelizeCompute.SetBuffer(0, "_Voxels", _staticVoxelsBuffer);
       _voxelizeCompute.Dispatch(0, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);

       long allTriangles = 0;
       // voxelization of the static objects in the scene
       foreach (Transform child in sceneToVoxelize.GetComponentsInChildren<Transform>()) {
           MeshFilter meshFilter = child.gameObject.GetComponent<MeshFilter>();

           if (!meshFilter)
           {
               continue;
           }
           Mesh sharedMesh = meshFilter.sharedMesh;

           // positions (x, y, z) of all vertices in the mesh
           _vertices = new ComputeBuffer(sharedMesh.vertexCount, 3 * sizeof(float));
           _vertices.SetData(sharedMesh.vertices);
           // indices defining how the vertices connect to form triangles
           _triangles = new ComputeBuffer(sharedMesh.triangles.Length, sizeof(int));
           _triangles.SetData(sharedMesh.triangles);
           
           
           // Debug.Log("Name of mesh: " + child.name);
           // Debug.Log("Number of vertices: " + sharedMesh.vertexCount);
           // Debug.Log("Number of triangles: " + sharedMesh.triangles.Length / 3);
           
           _voxelizeCompute.SetBuffer(4, "_MeshVertices", _vertices);
           _voxelizeCompute.SetBuffer(4, "_MeshTriangleIndices", _triangles);
           _voxelizeCompute.SetBuffer(4, "_StaticVoxels", _staticVoxelsBuffer);
           _voxelizeCompute.SetMatrix("_MeshLocalToWorld", child.localToWorldMatrix);
           _voxelizeCompute.SetInt("_numberOfTriangles", sharedMesh.triangles.Length);
           _voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
           _voxelizeCompute.SetVector("_BoundsExtent", boundsExtent);
           _voxelizeCompute.SetFloat("_VoxelSize", voxelSize);
           _voxelizeCompute.SetFloat("_IntersectionBias", intersectionBias);
           _voxelizeCompute.Dispatch(4, Mathf.CeilToInt( voxelsX / 8.0f), Mathf.CeilToInt(voxelsY/8.0f), Mathf.CeilToInt(voxelsZ/8.0f));
           
           allTriangles = allTriangles + sharedMesh.triangles.Length;
           
           _vertices.Release();
           _triangles.Release();
           
       }
       
       // Debug.Log("All triangles: "+ allTriangles/3);
       
       
       // args buffer for Graphics Rendering
       _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
       uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
       args[0] = (uint)debugMesh.GetIndexCount(0);
       args[1] = (uint)numberOfVoxels;
       args[2] = (uint)debugMesh.GetIndexStart(0);
       args[3] = (uint)debugMesh.GetBaseVertex(0);
       _argsBuffer.SetData(args);

       if (_selectedSmokeShape == SmokeShape.Plume)
       {
           _plumeSmoke = true;
           _cloudSmoke = false;
       }
   }
   
   public static float EasingFunction(float t)
   {
       float ease = 0.0f;
       if (t < 0.5f) 
           ease = 2 * t * t;
       else 
           ease = 1.0f - (1.0f / (5.0f * (2.0f * t - 0.8f) + 1));
       
       return Mathf.Min(1.0f, ease);
   }
   

    // Update is called once per frame
    void Update()
    {
        
        if (drawVoxelGrid)
        {
            // draw the full voxel grid resolution
            _debugAllVoxels = true;
            _debugSmokeVoxels = false;
            _debugStaticVoxels = false;
            _voxelGridVisualization.SetBuffer("_Voxels", _voxelsBuffer);
            _voxelGridVisualization.SetBuffer("_StaticVoxels", _staticVoxelsBuffer);
            _voxelGridVisualization.SetBuffer("_SmokeVoxels", _smokeVoxelsBuffer);
            _voxelGridVisualization.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            _voxelGridVisualization.SetVector("_BoundsExtent", boundsExtent);
            _voxelGridVisualization.SetFloat("_VoxelSize", voxelSize);
            _voxelGridVisualization.SetInt("_MaxFillSteps", MaxFillSteps);
            _voxelGridVisualization.SetInt("_DebugAllVoxels", _debugAllVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugSmokeVoxels", _debugSmokeVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugStaticVoxels", _debugStaticVoxels ? 1 : 0);
            
            Graphics.DrawMeshInstancedIndirect(debugMesh, 0, _voxelGridVisualization, debugBounds, _argsBuffer);
        }
        
        // static scene rendering with voxels
        if (drawStaticScene)
        {
            _debugStaticVoxels = true;
            _debugAllVoxels = false;
            _debugSmokeVoxels = false;
            _voxelGridVisualization.SetBuffer("_Voxels", _voxelsBuffer);
            _voxelGridVisualization.SetBuffer("_StaticVoxels", _staticVoxelsBuffer);
            _voxelGridVisualization.SetBuffer("_SmokeVoxels", _smokeVoxelsBuffer);
            _voxelGridVisualization.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            _voxelGridVisualization.SetVector("_BoundsExtent", boundsExtent);
            _voxelGridVisualization.SetFloat("_VoxelSize", voxelSize);
            _voxelGridVisualization.SetInt("_MaxFillSteps", MaxFillSteps);
            _voxelGridVisualization.SetInt("_DebugAllVoxels", _debugAllVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugSmokeVoxels", _debugSmokeVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugStaticVoxels", _debugStaticVoxels ? 1 : 0);
            
            Graphics.DrawMeshInstancedIndirect(debugMesh, 0, _voxelGridVisualization, debugBounds, _argsBuffer);
            

        }
        
        // smoke
        if (Input.GetMouseButton(0))
        {
            _voxelizeCompute.SetBuffer(0, "_Voxels", _smokeVoxelsBuffer);
            _voxelizeCompute.Dispatch(0, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);
       
            _voxelizeCompute.SetBuffer(0, "_Voxels", _floodFillSmokeBuffer);
            _voxelizeCompute.Dispatch(0, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); 
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 300))
            {
                _radius = 0.0f;
                _height = 0.0f;
                maxPlumeHeight = new Vector3(boundsExtent.y * 2, boundsExtent.y * 2, boundsExtent.y * 2);
                maxRadius = new Vector3(5, 3, 5);
                _smokeOrigin = hit.point;
                // _smokeOrigin = new Vector3(0, 0, 0);
                // Debug.Log("Smoke origin: " + _smokeOrigin);
                _voxelizeCompute.SetVector("_SmokeOrigin", _smokeOrigin);
                _voxelizeCompute.SetBuffer(2, "_SmokeVoxels", _smokeVoxelsBuffer);
                _voxelizeCompute.SetInt("_MaxFillStep", MaxFillSteps);
                // fill smoke origin in the voxel grid
                _voxelizeCompute.Dispatch(2, Mathf.CeilToInt(numberOfVoxels / 128.0f), 1, 1);

                _smokeConstantFill = true;
                _smokeIterateFill = true;
            }
        }
            
        /*if (_smokeConstantFill)
        {
            // fill the smoke voxel grid at once
            _radius = 5;
            // send radius to compute shader
            _voxelizeCompute.SetVector("_Radius", new Vector3(_radius, _radius-2, _radius));
            _voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            _voxelizeCompute.SetBuffer(3, "_SmokeVoxels", _smokeVoxelsBuffer);
            _voxelizeCompute.SetBuffer(3, "_FloodFillVoxels", _floodFillSmokeBuffer);
            _voxelizeCompute.Dispatch(3, voxelsX, voxelsY, voxelsZ);
            
        }*/

        if (_smokeIterateFill) {
            if (_plumeSmoke)
            {
                _voxelizeCompute.SetVector("_SmokeOrigin", _smokeOrigin);
                _voxelizeCompute.SetVector("_BoundsExtent", boundsExtent);
                _voxelizeCompute.SetVector("_PlumeHeight", Vector3.Lerp(Vector3.zero, maxPlumeHeight, EasingFunction(_height)));
                //_voxelizeCompute.SetVector("_PlumeHeight", maxPlumeHeight);
                _voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
                _voxelizeCompute.SetBuffer(7, "_SmokeVoxels", _smokeVoxelsBuffer);
                _voxelizeCompute.Dispatch(7, Mathf.CeilToInt(voxelsX/8.0f), Mathf.CeilToInt(voxelsY/8.0f), Mathf.CeilToInt(voxelsZ/8.0f));
                
                // gradually increase radius over time
                _height += smokeGrowthSpeed * Time.deltaTime; 
            }
            else
            {
               // animate smoke voxel grid
                _voxelizeCompute.SetVector("_Radius", Vector3.Lerp(Vector3.zero, maxRadius, EasingFunction(_radius)));
                _voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
                _voxelizeCompute.SetBuffer(3, "_SmokeVoxels", _smokeVoxelsBuffer);
                _voxelizeCompute.SetBuffer(3, "_StaticVoxels", _staticVoxelsBuffer);
                _voxelizeCompute.SetBuffer(3, "_FloodFillVoxels", _floodFillSmokeBuffer);
                _voxelizeCompute.Dispatch(3, Mathf.CeilToInt(voxelsX/8.0f), Mathf.CeilToInt(voxelsY/8.0f), Mathf.CeilToInt(voxelsZ/8.0f));
                           
                _voxelizeCompute.SetBuffer(5, "_SmokeVoxels", _smokeVoxelsBuffer);
                _voxelizeCompute.SetBuffer(5, "_FloodFillVoxels", _floodFillSmokeBuffer);
                _voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
                _voxelizeCompute.Dispatch(5, Mathf.CeilToInt(voxelsX/8.0f), Mathf.CeilToInt(voxelsY/8.0f), Mathf.CeilToInt(voxelsZ/8.0f));
                // gradually increase radius over time
                _radius += smokeGrowthSpeed * Time.deltaTime; 
            }
            
            
        }
        
        
        // render smoke with voxels
        if (drawSmoke)
        {
            
            _debugSmokeVoxels = true;
            _debugAllVoxels = false;
            _voxelGridVisualization.SetBuffer("_Voxels", _voxelsBuffer);
            _voxelGridVisualization.SetBuffer("_StaticVoxels", _staticVoxelsBuffer);
            _voxelGridVisualization.SetBuffer("_SmokeVoxels", _smokeVoxelsBuffer);
            _voxelGridVisualization.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            _voxelGridVisualization.SetVector("_BoundsExtent", boundsExtent);
            _voxelGridVisualization.SetFloat("_VoxelSize", voxelSize);
            _voxelGridVisualization.SetInt("_MaxFillSteps", MaxFillSteps);
            _voxelGridVisualization.SetInt("_DebugAllVoxels", _debugAllVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugSmokeVoxels", _debugSmokeVoxels ? 1 : 0);
            _voxelGridVisualization.SetInt("_DebugStaticVoxels", _debugStaticVoxels ? 1 : 0);
            
            Graphics.DrawMeshInstancedIndirect(debugMesh, 0, _voxelGridVisualization, debugBounds, _argsBuffer);
            
        }
        
    }
    
    Vector3 CalculateVoxelPosition(int index)
    {
        // calculating position of voxel based on index for use of gizmo visualization
        Vector3 positionOffet = new Vector3(0, boundsExtent.y, 0);
        Vector3 boundsSize = boundsExtent * 2;
        voxelsX = Mathf.CeilToInt(boundsSize.x / voxelSize);
        voxelsY = Mathf.CeilToInt(boundsSize.y / voxelSize);
        int x = index % voxelsX;
        int y = (index / voxelsX) % voxelsY;
        int z = index / (voxelsX * voxelsY);

        Vector3 voxelPosition = transform.position + positionOffet + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize - boundsExtent;

        return voxelPosition;
    }
   
    // draw bounding box / voxel grid - gizmo visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Vector3 positionOffet = new Vector3(0, boundsExtent.y, 0);
            // bounding box wireframe
            if (drawBoxGizmo)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + positionOffet, boundsExtent * 2);  
            }

            // voxel grid
            Vector3 voxelPosition = new Vector3(0, 0, 0);

            if (drawVoxelGridGizmo)
            {
                for (int i = 0; i < 43200; i++)
                {
                    voxelPosition = CalculateVoxelPosition(i);
                    // Debug.Log(voxelPosition);
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(voxelPosition, Vector3.one * voxelSize);
                }

            }
        }
    }

    private void OnDisable()
    {
        _voxelsBuffer.Release();
        _staticVoxelsBuffer.Release();
        _smokeVoxelsBuffer.Release();
        _argsBuffer.Release();
        _vertices.Release();
        _triangles.Release();
        
    }
    
    private void OnApplicationQuit()
    {
        _staticVoxelsBuffer.Dispose();
        _smokeVoxelsBuffer.Dispose();
        _argsBuffer.Dispose();
        _voxelsBuffer.Dispose();
        _vertices.Dispose();
        _triangles.Dispose();
    }
}
