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

    [Range(0.0f, 1.0f)]
    public float alphaThreshold = 0.1f;
    
    public Color extinctionColor = new Color(1, 1, 1);
    

    [Range(0.0f, 10.0f)] 
    // public float shadowDensity = 1.0f;

    private RenderTexture smokeTex, smokeMaskTex;
    private RenderTexture noiseTex, depthTex, smokeAlbedoFullTex, smokeMaskFullTex;
    
    
    private void OnEnable()
    {
        // material
        _smokeMaterial = new Material(Shader.Find("Custom/Effects"));
        // compute shader
        _raymarchingCompute = (ComputeShader)Resources.Load("VoxelGrid");

        _camera = GetComponent<Camera>();
        _light = GameObject.Find("Area Light");
        
        
        // TODO initialize noise variables
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
        
        // TODO update noise 
        
        // get smoke voxels to use data in ray marching
        if (smokeVoxels != null)
        {
            _smokeVoxelsBuffer = smokeVoxels.GetSmokeVoxelBuffer();
            _boundsExtent = smokeVoxels.GetBoundsExtent();
            _voxelResolution = smokeVoxels.GetVoxelResolution();
            _raymarchingCompute.SetBuffer(0, "_SmokeVoxels", _smokeVoxelsBuffer);
            _raymarchingCompute.SetVector("_BoundsExtent", _boundsExtent);
            _raymarchingCompute.SetVector("_VoxelResolution", _voxelResolution);
        }
        
    }
    
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        smokeTex = smokeAlbedoFullTex;
        smokeMaskTex = smokeMaskFullTex;
        Graphics.Blit(source, depthTex, _smokeMaterial, 0); // creating depth texture

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
        Matrix4x4 viewProjectionMatrix = projectionMatrix * _camera.worldToCameraMatrix;
        
        // applying post-processing effects to the rendered image
        // Graphics.Blit(source, destination);
        
        // TODO set all compute shader values
        _raymarchingCompute.SetInt("_TargetBufferWidth", smokeTex.width);
        _raymarchingCompute.SetInt("_TargetBufferHeight", smokeTex.height);
        _raymarchingCompute.SetTexture(0, "_SmokeTex", smokeTex);
        _raymarchingCompute.SetTexture(0, "_DepthTex", depthTex);
        _raymarchingCompute.SetTexture(0, "_SmokeMaskTex", smokeMaskTex);
        // camera
        _raymarchingCompute.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        _raymarchingCompute.SetMatrix("_CameraInverseProjection", projectionMatrix.inverse);
        _raymarchingCompute.SetMatrix("_CameraInverseViewProjection", viewProjectionMatrix.inverse);
        _raymarchingCompute.SetVector("_CameraWorldPosition", this.transform.position);
        // light
        _raymarchingCompute.SetVector("_LightColor", lightColor);
        // raymarching parameters
        _raymarchingCompute.SetVector("_SmokeColor", smokeColor);
        _raymarchingCompute.SetVector("_ExtinctionColor", extinctionColor);
        _raymarchingCompute.SetFloat("_VolumeDensity", volumeDensity);
        _raymarchingCompute.SetInt("_totalSteps", stepCount);
        _raymarchingCompute.SetFloat("_stepSize", stepSize);
        _raymarchingCompute.SetFloat("_LightStepSize", lightStepSize);
        _raymarchingCompute.SetFloat("_AbsorptionCoefficient", absorptionCoefficient);
        _raymarchingCompute.SetFloat("_ScatteringCoefficient", scatteringCoefficient);
        _raymarchingCompute.SetFloat("_AlphaThreshold", alphaThreshold);
        
         
        // TODO dispatch ray marching compute shader kernel
        _raymarchingCompute.Dispatch(0, Mathf.CeilToInt(smokeTex.width / 8.0f), Mathf.CeilToInt(smokeTex.height / 8.0f), 1);
       
        // composite effects
        _smokeMaterial.SetTexture("_SmokeTex", smokeTex);
        _smokeMaterial.SetTexture("_SmokeMaskTex", smokeMaskTex);
        _smokeMaterial.SetTexture("_DepthTex", depthTex);
        _smokeMaterial.SetFloat("_Sharpness", 0.1f);
        _smokeMaterial.SetFloat("_DebugView", 3);
        Graphics.Blit(source, destination, _smokeMaterial, 2);
        
       
        // TODO set shader values to build final imageGraphics.Blit(source, destination);


    }
}

