Shader "Custom/VisualizeVoxels1" {
	SubShader {

		Pass {
            Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

			#pragma vertex vp
			#pragma fragment fp

			#include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            StructuredBuffer<int> _SmokeVoxels;
            StructuredBuffer<int> _StaticVoxels;
			StructuredBuffer<int> _Voxels;
			float3 _positionOffset;
            float3 _BoundsExtent;
            uint3 _VoxelResolution;
            float _VoxelSize;
            int _MaxFillSteps, _DebugSmokeVoxels, _DebugAllVoxels, _DebugStaticVoxels, _DebugEdgeVoxels;

			struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
                float3 hashCol : TEXCOORD0;
				float3 normal : TEXCOORD1;
			};
			
            float grayscaleHash(uint n) {
                n = (n << 13U) ^ n;
			    n = n * (n * n * 15731U + 0x789221U) + 0x1376312589U;
				
			    // return value between 0 and 1
			    return frac(sin(float(n)) * 43758.5453123);	
            }

			// convert one dimensional index to three dimensional position in the voxel grid
            // i ranges from 0 to _VoxelResolution - 1
			v2f vp(VertexData v, uint instanceID : SV_INSTANCEID) {
				v2f i;
                
                uint x = instanceID % (_VoxelResolution.x); // x = i mod width
                uint y = (instanceID / _VoxelResolution.x) % (_VoxelResolution.y); // y = i/width mod height
                uint z = instanceID / (_VoxelResolution.x * _VoxelResolution.y); // z = i / width*height

                // to get world space, we multiply the position by the size of the voxel and substract the bounding box extent of the grid
				i.pos = UnityObjectToClipPos((v.vertex + float3(x, y, z)) * _VoxelSize + _positionOffset + (_VoxelSize * 0.5f) - _BoundsExtent);

				if (_DebugSmokeVoxels)
					//i.pos *= saturate(_SmokeVoxels[instanceID]);
            		i.pos *= _SmokeVoxels[instanceID];
				if (_DebugStaticVoxels)
					i.pos *= _StaticVoxels[instanceID];
            	if (_DebugAllVoxels)
            		i.pos *= _Voxels[instanceID];

				
				i.normal = UnityObjectToWorldNormal(v.normal);
                i.hashCol = float3(grayscaleHash(instanceID), grayscaleHash(instanceID), grayscaleHash(instanceID));
				
				return i;
			}

			float4 fp(v2f i) : SV_TARGET {
                float3 ndotl = DotClamped(_WorldSpaceLightPos0.xyz, i.normal) * 0.5f + 0.5f;
                ndotl *= ndotl;
				
				return float4(i.hashCol * ndotl, 1.0f);

			}

			ENDCG
		}
	}
}