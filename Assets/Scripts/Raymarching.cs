using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Raymarching : MonoBehaviour
{
    private GameObject _light;
    private Camera _camera;
    private ComputeBuffer _smokeVoxelsBuffer;
    private Vector3 _boundsExtent;
    private Vector3 _voxelResolution;

    public VoxelGrid smokeVoxels = null;

    private Material _smokeMaterial;
    private ComputeShader _raymarchingCompute;
    
    public Color lightColor;
    
    // parameters for noise generation
    [Header("Noise settings")] 
    [Space(5)] 
    public int seed;

   
    // parameters for smoke rendering
    [Header("Smoke settings")] 
    [Space(5)] 
    public Color smokeColor;
    
    [Space(5)]
    // number of raymarch steps
    [Range(1, 265)]
    public int stepCount = 32;

    // size of raymarch step
    [Range(0.01f, 0.1f)]
    public float stepSize = 0.05f;
    
    [Space(5)] 
    // outscattering
    // number of light steps
    [Range(1, 32)]
    public int lightStepCount = 8;
    // size of light step
    [Range(0.01f, 0.1f)]
    public float lightStepSize = 0.05f;

    [Range(0.01f, 64.0f)] 
    public float smokeSize = 32.0f;

    public float volumeDensity = 1.0f;
    
    // absorption and scattering
    [Range(0.0f, 3.0f)] 
    public float absorptionCoefficient = 0.5f;
    [Range(0.0f, 3.0f)] 
    public float scatteringCoefficient = 0.5f;

    public Color extinctionColor = new Color(1, 1, 1);

    [Range(0.0f, 10.0f)] 
    public float shadowDensity = 1.0f;


    private RenderTexture noiseTex, depthTex, smokeAlbedoFullTex, smokeMaskFullTex;
    
    
    private void OnEnable()
    {
        // material
        _smokeMaterial = new Material(Shader.Find("Custom/Effects"));
        // compute shader
        _raymarchingCompute = (ComputeShader)Resources.Load("VoxelGrid");

        _camera = GetComponent<Camera>();
        _light = GameObject.Find("Area Light");
        
        
        // initialize noise variables
        if (noiseTex != null)
        {
            // update noise 
        }
        else
        {
            // TODO: create new render texture
        }
        
        // initialize all the variables for smoke rendering
        // render textures for albedo and smoke mask
        // rgba
        smokeAlbedoFullTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64,
            RenderTextureReadWrite.Linear);
        smokeAlbedoFullTex.enableRandomWrite = true;
        smokeAlbedoFullTex.Create();

        // single (red) channel
        smokeMaskFullTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        smokeMaskFullTex.enableRandomWrite = true;
        smokeMaskFullTex.Create();

        // single channel for depth
        depthTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        depthTex.enableRandomWrite = true;
        depthTex.Create();

    }


    void Update()
    {
        
        // update noise 
        
        // get smoke voxels to use data in ray marching
        if (smokeVoxels != null)
        {
            _smokeVoxelsBuffer = smokeVoxels.GetSmokeVoxelBuffer();
            _boundsExtent = smokeVoxels.GetBoundsExtent();
            _voxelResolution = smokeVoxels.GetVoxelResolution();
        }
        
        

        
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        
        
        Graphics.Blit(source, destination, _smokeMaterial, 0);
        
        
    }
}

