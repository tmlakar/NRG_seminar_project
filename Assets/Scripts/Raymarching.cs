using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Must;

[RequireComponent(typeof(Camera))]
public class Raymarching : MonoBehaviour
{
    private GameObject _light;
    private Camera _camera;
    private ComputeBuffer _smokeVoxelsBuffer;
    private Vector3 _boundsExtent;
    private Vector3 _voxelResolution;

    private Material _smokeMaterial;
    private ComputeShader _raymarchingCompute;
    
    public VoxelGrid smokeVoxels;
    
    public Color lightColor;
    private Vector3 _lightPosition;
    // parameters for noise generation
    // [Header("Noise settings")] 
    // [Space(5)] 
    // public int seed;

   
    // parameters for smoke rendering
    [Header("Smoke rendering settings")] 
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

    public float shadowDensity = 1.0f;
    [Range(0.0f, 3.0f)] 
    public float scatteringCoefficient = 0.5f;
    [Range(0.0f, 1.0f)]
    public float alphaThreshold = 0.1f;
    public Color extinctionColor = new Color(1, 1, 1);
    

    private RenderTexture smokeTexQRes, smokeMaskTexQRes, smokeTexHRes, smokeMaskTexHRes, smokeTexScreenRes, smokeMaskTexScreenRes;
    private RenderTexture noiseTex, depthTex;
    private Texture2D debugTexture;
    
    [Space(10)] 
    public ViewTexture viewTexture;
    // rendering settings
    public enum ViewTexture
    {
        Composite = 0,
        SmokeAlbedo = 1,
        SmokeMask = 2,
        Depth = 3
    }

    public bool createRenders = false;

    
    void debugSmokeMaskTex()
    {
        RenderTexture.active = smokeMaskTexQRes;
        debugTexture.ReadPixels(new Rect(0, 0, smokeMaskTexQRes.width, smokeMaskTexQRes.height), 0, 0);
        debugTexture.Apply();
        Color[] pixels = debugTexture.GetPixels();
        int allPixels = smokeMaskTexQRes.width * smokeMaskTexQRes.height;
        int numberOfNonZero = 0;
        for (int y = 0; y < debugTexture.height; y++) {
            for (int x = 0; x < debugTexture.width; x++) {
                float depthValue = pixels[y * debugTexture.width + x].r;
                if (depthValue == 1.0f)
                {
                    numberOfNonZero = numberOfNonZero + 1;
                    //Debug.Log($"Mask value at ({x}, {y}): {depthValue}");
                }
            }
        }
        Debug.Log("All pixels: "+ allPixels);
        Debug.Log("Number of non zero:" + numberOfNonZero);
        Debug.Log("Percentage of screen covered:" + 100*((float)numberOfNonZero/(float)allPixels));
        RenderTexture.active = null;
    }

    void SaveSceneRenderToPNG(String filepath)
    {
        Camera cam = GetComponent<Camera>();
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = cam.targetTexture;
        
        cam.Render();
        
        Texture2D sceneTexture = new Texture2D(Screen.width, Screen.height);
        sceneTexture.ReadPixels(new Rect(0, 0, sceneTexture.width, sceneTexture.height), 0, 0);
        sceneTexture.Apply();
        RenderTexture.active = currentRT;

        var Bytes = sceneTexture.EncodeToPNG();
        Destroy(sceneTexture);
        File.WriteAllBytes(filepath, Bytes);

    }
    void SaveRenderTextureToPNG(RenderTexture renderTexture, String filepath)
    {
        
        Texture2D currentTexture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA64, false);
        RenderTexture.active = renderTexture;
        
        Texture2D texture2D;
        if (renderTexture.format == RenderTextureFormat.RFloat)
        {
            // Handle RFloat format
            texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RFloat, false);
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();

            // Create a new texture to store the R channel as grayscale
            Texture2D grayscaleTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

            for (int y = 0; y < renderTexture.height; y++)
            {
                for (int x = 0; x < renderTexture.width; x++)
                {
                    float rValue = 1 - texture2D.GetPixel(x, y).r;
                    Color grayscaleColor = new Color(rValue, 0, 0); // Red channel value to grayscale
                    grayscaleTexture.SetPixel(x, y, grayscaleColor);
                }
            }

            grayscaleTexture.Apply();
            byte[] bytes = grayscaleTexture.EncodeToPNG();
            File.WriteAllBytes(filepath, bytes);
            Destroy(grayscaleTexture);
        }
        else
        { 
            currentTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0); 
            currentTexture2D.Apply();
                  
          byte[] bytes = currentTexture2D.EncodeToPNG();
          File.WriteAllBytes(filepath, bytes);
          
        }
        RenderTexture.active = null;
        Destroy(currentTexture2D);
        Debug.Log("RenderedTexture saved to: " + filepath);  
    }
    
    private void OnEnable()
    {
        // material
        _smokeMaterial = new Material(Shader.Find("Custom/Effects"));
        // compute shader
        _raymarchingCompute = (ComputeShader)Resources.Load("Raymarching");

        _camera = GetComponent<Camera>();
        _light = GameObject.Find("Area Light");
        _camera.depthTextureMode = DepthTextureMode.Depth;
        _lightPosition = _light.transform.position;
        
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
        smokeTexQRes = new RenderTexture(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.ARGB64,
            RenderTextureReadWrite.Linear);
        smokeTexQRes.enableRandomWrite = true;
        smokeTexQRes.Create();
        
        smokeTexHRes = new RenderTexture(Screen.width/2, Screen.height/2, 0, RenderTextureFormat.ARGB64,
            RenderTextureReadWrite.Linear);
        smokeTexHRes.enableRandomWrite = true;
        smokeTexHRes.Create();

        smokeTexScreenRes = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64,
            RenderTextureReadWrite.Linear);
        smokeTexScreenRes.enableRandomWrite = true;
        smokeTexScreenRes.Create();
        

        // single (red) channel
        smokeMaskTexQRes = new RenderTexture(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        smokeMaskTexQRes.enableRandomWrite = true;
        smokeMaskTexQRes.Create();
        
        smokeMaskTexHRes = new RenderTexture(Screen.width/2, Screen.height/2, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        smokeMaskTexHRes.enableRandomWrite = true;
        smokeMaskTexHRes.Create();
        
        smokeMaskTexScreenRes = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        smokeMaskTexScreenRes.enableRandomWrite = true;
        smokeMaskTexScreenRes.Create();

        // single channel for depth
        depthTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat,
            RenderTextureReadWrite.Linear);
        depthTex.enableRandomWrite = true;
        depthTex.Create();
        
        debugTexture = new Texture2D(depthTex.width, depthTex.height, TextureFormat.RFloat, false);
        
        // initialize
        _raymarchingCompute.SetTexture(2, "_SmokeTex", smokeTexQRes);
        _raymarchingCompute.Dispatch(2, Mathf.CeilToInt(smokeTexQRes.width/32.0f), Mathf.CeilToInt(smokeTexQRes.height), 1);

        _raymarchingCompute.SetTexture(2, "_SmokeTex", smokeTexHRes);
        _raymarchingCompute.Dispatch(2, Mathf.CeilToInt(smokeTexHRes.width/32.0f), Mathf.CeilToInt(smokeTexHRes.height), 1);

        _raymarchingCompute.SetTexture(2, "_SmokeTex", smokeTexScreenRes);
        _raymarchingCompute.Dispatch(2, Mathf.CeilToInt(smokeTexScreenRes.width/32.0f), Mathf.CeilToInt(smokeTexScreenRes.height), 1);

        
        _raymarchingCompute.SetTexture(1, "_SmokeMaskTex", smokeMaskTexQRes);
        _raymarchingCompute.Dispatch(1, Mathf.CeilToInt(smokeMaskTexQRes.width/32.0f), Mathf.CeilToInt(smokeMaskTexQRes.height), 1);

        _raymarchingCompute.SetTexture(1, "_SmokeMaskTex", smokeMaskTexHRes);
        _raymarchingCompute.Dispatch(1, Mathf.CeilToInt(smokeMaskTexHRes.width/32.0f), Mathf.CeilToInt(smokeMaskTexHRes.height), 1);
        
        _raymarchingCompute.SetTexture(1, "_SmokeMaskTex", smokeMaskTexScreenRes);
        _raymarchingCompute.Dispatch(1, Mathf.CeilToInt(smokeMaskTexScreenRes.width/32.0f), Mathf.CeilToInt(smokeMaskTexScreenRes.height), 1);
        
    }


    void Update()
    {
        
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
        // applying post-processing effects to the rendered image
        
        // depth texture
        Graphics.Blit(source, depthTex, _smokeMaterial, 0);

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, false);
        Matrix4x4 viewProjectionMatrix = projectionMatrix * _camera.worldToCameraMatrix;
        
        
        // set all compute shader values
        _raymarchingCompute.SetBuffer(0, "_SmokeVoxels", _smokeVoxelsBuffer);
        _raymarchingCompute.SetInt("_TargetBufferWidth", smokeTexQRes.width);
        _raymarchingCompute.SetInt("_TargetBufferHeight", smokeTexQRes.height);
        _raymarchingCompute.SetTexture(0, "_SmokeTex", smokeTexQRes);
        _raymarchingCompute.SetTexture(0, "_DepthTex", depthTex);
        _raymarchingCompute.SetTexture(0, "_SmokeMaskTex", smokeMaskTexQRes);
        // camera
        _raymarchingCompute.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        _raymarchingCompute.SetMatrix("_CameraInverseProjection", projectionMatrix.inverse);
        _raymarchingCompute.SetMatrix("_CameraInverseViewProjection", viewProjectionMatrix.inverse);
        _raymarchingCompute.SetVector("_CameraWorldPosition", this.transform.position);
        // light
        _raymarchingCompute.SetVector("_LightColor", lightColor);
        _raymarchingCompute.SetVector("_LightPosition", _lightPosition);
        // raymarching parameters
        _raymarchingCompute.SetVector("_SmokeColor", smokeColor);
        _raymarchingCompute.SetVector("_ExtinctionColor", extinctionColor);
        _raymarchingCompute.SetFloat("_VolumeDensity", volumeDensity);
        _raymarchingCompute.SetFloat("_ShadowDensity", shadowDensity);
        _raymarchingCompute.SetInt("_totalSteps", stepCount);
        _raymarchingCompute.SetInt("_LightTotalSteps", lightStepCount);
        _raymarchingCompute.SetFloat("_stepSize", stepSize);
        _raymarchingCompute.SetFloat("_LightStepSize", lightStepSize);
        _raymarchingCompute.SetFloat("_AbsorptionCoefficient", absorptionCoefficient);
        _raymarchingCompute.SetFloat("_ScatteringCoefficient", scatteringCoefficient);
        _raymarchingCompute.SetFloat("_AlphaThreshold", alphaThreshold);
        
        /*
        // binary ray march where SmokeMaskTex is set to 1 if encountered voxel
        _raymarchingCompute.SetTexture(3, "_SmokeMaskTex", smokeMaskTexQRes);
        _raymarchingCompute.SetBuffer(3, "_SmokeVoxels", _smokeVoxelsBuffer);
        _raymarchingCompute.SetInt("_TargetBufferWidth", smokeMaskTexQRes.width);
        _raymarchingCompute.SetInt("_TargetBufferHeight", smokeMaskTexQRes.height);
        _raymarchingCompute.Dispatch(3, Mathf.CeilToInt(smokeMaskTexQRes.width / 8.0f), Mathf.CeilToInt(smokeMaskTexQRes.height / 8.0f), 1);
        */
        _raymarchingCompute.Dispatch(0, Mathf.CeilToInt(smokeMaskTexQRes.width / 8.0f), Mathf.CeilToInt(smokeMaskTexQRes.height / 8.0f), 1);

        // supersampling
        // enlarge the textures before compositing the effects
        
        // default bilinear interpolation
        Graphics.Blit(smokeMaskTexQRes, smokeMaskTexHRes);
        Graphics.Blit(smokeMaskTexHRes, smokeMaskTexScreenRes);
        Graphics.Blit(smokeMaskTexScreenRes, smokeMaskTexHRes);
        Graphics.Blit(smokeMaskTexHRes, smokeMaskTexQRes);

        // bicubic interpolation 
        
        // composite effects
        // set shader values to build final image Graphics.Blit(source, destination);
        _smokeMaterial.SetTexture("_SmokeTex", smokeTexQRes);
        _smokeMaterial.SetTexture("_SmokeMaskTex", smokeMaskTexScreenRes);
        _smokeMaterial.SetTexture("_DepthTex", depthTex);
        _smokeMaterial.SetFloat("_Sharpness", 0.0f);
        _smokeMaterial.SetFloat("_DebugView", (int)viewTexture);
        Graphics.Blit(source, destination, _smokeMaterial, 1);

        if (createRenders)
        {
            
            //SaveRenderTextureToPNG(depthTex, "RenderedImages/sceneDepthMask.png");
            SaveRenderTextureToPNG(smokeMaskTexScreenRes, "RenderedImages/smokeMask.png");
            SaveRenderTextureToPNG(smokeTexScreenRes, "RenderedImages/smokeAlbedo.png");
            SaveSceneRenderToPNG("RenderedImages/scene.png");
            //debugSmokeMaskTex();
            //debugDepthTex();
            createRenders = false;
        }
        
    }

    private void OnDisable()
    {
        smokeTexQRes.Release();
        smokeTexHRes.Release();
        smokeTexScreenRes.Release();
        smokeMaskTexQRes.Release();
        smokeMaskTexHRes.Release();
        smokeMaskTexScreenRes.Release();
        depthTex.Release();
    }
}

