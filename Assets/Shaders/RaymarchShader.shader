Shader "Raymarch/RaymarchShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "DistanceFunctions.cginc"

            sampler2D           _MainTex;

            // Setup
            uniform sampler2D   _CameraDepthTexture;
            uniform float4x4    CamFrustum, CamToWorld;
            uniform int         maxIterations;
            uniform float       maxDistance, accuracy;

            // Color
            uniform fixed4      groundColor;
            uniform fixed4      sphereColor[8];
            uniform float       colorIntensity;

            // Light
            uniform float3      lightDir, lightCol;
            uniform float       lightIntensity;

            // Shadow
            uniform float2      shadowDistance;
            uniform float       shadowIntensity, shadowPenumbra;

            // Reflection
            uniform int         reflectionCount;
            uniform float       reflectionIntensity, envReflIntensity;
            uniform samplerCUBE reflectionCube;

            // SDF
            uniform float4      sphere;
            uniform float       sphereSmooth;
            uniform float       degreeRotate;
            /*uniform float     boxSphereSmooth, sphereIntersectSmooth;
            uniform float4      sphere1, sphere2, box1;*/
            uniform float3      modInterval;            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ray : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                half index = v.vertex.z;
                v.vertex.z = 0;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                o.ray = CamFrustum[(int)index].xyz;

                o.ray /= abs(o.ray.z);

                o.ray = mul(CamToWorld, o.ray);

                return o;
            }

            /*float BoxSphere(float3 p)
            {
                float Sphere1 = sdSphere(p - sphere1.xyz, sphere1.w);
                float Box1 = sdRoundBox(p - box1.xyz, box1.www, box1Round);
                float combine1 = opSS(Sphere1, Box1, boxSphereSmooth);

                float Sphere2 = sdSphere(p - sphere2.xyz, sphere2.w);
                float combine2 = opIS(Sphere2, combine1, sphereIntersectSmooth);

                return combine2;
            }
            */
            float3 rotateY(float3 v, float degree)
            {
                float rad = degree * 0.0174532925;  // PI / 180;
                float cosY = cos(rad);
                float sinY = sin(rad);

                return float3(cosY * v.x - sinY * v.z, v.y, sinY * v.x + cosY * v.z);
            }

            // p: Position
            float4 distanceField(float3 p)
            {
               /* float modX = pMod1(p.x, modInterval.x);
                float modY = pMod1(p.y, modInterval.y);
                float modZ = pMod1(p.z, modInterval.z);*/

                //float boxSphere1 = BoxSphere(p);

                float4 result;

                float4 ground = float4(groundColor.rgb, sdPlane(p, float4(0, 1, 0, 0)));
                float4 spheres = float4(sphereColor[0].rgb, sdSphere(p - sphere.xyz, sphere.w));
                
                for (int i = 1; i < 8; i++)
                {
                    float4 sphereAdd = float4(sphereColor[i].rgb, sdSphere(rotateY(p, degreeRotate * i) - sphere.xyz, sphere.w));
                    spheres = opU(spheres, sphereAdd);
                }

                float4 torus = float4(fixed3(0, 255, 0), sdTorus(p - float4(0, 4, 0, 50).xyz, float2(3, 1)));

                result = opUS(spheres, ground, sphereSmooth);
                result = opU(result, torus);
                
                return result;
            }

            float3 getNormal(float3 p)
			{
                const float2 offset = float2(0.001, 0);
                float3 normal = float3(
                    distanceField(p + offset.xyy).w - distanceField(p - offset.xyy).w,
                    distanceField(p + offset.yxy).w - distanceField(p - offset.yxy).w,
                    distanceField(p + offset.yyx).w - distanceField(p - offset.yyx).w);

                return normalize(normal);
            }

            float hardShadow(float3 rayOrigin, float3 rayDirection, float mint, float maxt)
            {
                for (float t = mint, h; t < maxt; t+= h)
                {
                    h = distanceField(rayOrigin + rayDirection * t).w;

                    if (h < 0.001)
                        return 0;
                }

                return 1;
            }

            float softShadow(float3 rayOrigin, float3 rayDirection, float mint, float maxt, float k)
            {
                float result = 1;
                for (float t = mint, h; t < maxt; t+= h)
                {
                    h = distanceField(rayOrigin + rayDirection * t).w;

                    if (h < 0.001)
                        return 0;

                    result = min(result, k * h / t);
                }

                return result;
            }

            uniform float   aoStepsize, aoIntensity;
            uniform int     aoIterations;

            float ambientOcclusion(float3 p, float3 n)
            {
                float step = aoStepsize;
                float ao = 0;
                float dist;

                for (int i = 1; i <= aoIterations; i++)
			    {
                    dist = step * i;
                    ao += max(0, (dist - distanceField(p + n * dist).w) / dist);
                }
                return 1 - ao * aoIntensity;
            }

            float3 shading(float3 p, float3 normal, fixed3 c)
            {
                // Direction of light
                float3 light = (lightCol * dot(-lightDir, normal) * 0.5 + 0.5) * lightIntensity;

                // Shadows
                float shadow = softShadow(p, -lightDir, shadowDistance.x, shadowDistance.y, shadowPenumbra) * 0.5 + 0.5;

                shadow = max(0, pow(shadow, shadowIntensity));

                // Ambient Occlusion
                float ao = ambientOcclusion(p, normal);

                // Diffuse color
                float3 color = c.rgb * colorIntensity;

                return color * light * shadow * ao;
            }

            // ro : Ray Origin, rd : Ray Direction
            bool raymarching(float3 ro, float3 rd, float depth, float _maxDistance, int _maxIterations, inout float3 p, inout fixed3 dColor)
			{
                float t = 0; // Distance travelled along the ray direction
                float4 d;
                for (int i = 0; i < _maxIterations; i++, t += d.w)
                {
                    if (t > _maxDistance || t >= depth) // Pour render les GameObjects devant les UVs
                        return false;

                    // Get the actual position
                    p = ro + rd * t;

                    // Check for hit in distancefield
                    d = distanceField(p);

                    if (d.w < accuracy) // We have hit something
                    {
                        dColor = d.rgb;
                        return true;
                    }
                }

                return false;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, i.uv).r);
                depth *= length(i.ray);
                fixed3  color = tex2D(_MainTex, i.uv);
                float3  rayDirection = normalize(i.ray.xyz);
                float3  rayOrigin = _WorldSpaceCameraPos;
                fixed4  result;
                float3  hitPosition;
                fixed3  dColor;

                bool hit = raymarching(rayOrigin, rayDirection, depth, maxDistance, maxIterations, hitPosition, dColor);

                if (hit)
                {
                    float3  normal = getNormal(hitPosition);
                    float3  s = shading(hitPosition, normal, dColor);

                    result = fixed4(s, 1);

                    uint    mipLevel = 2;

                    for (int i = 0; i < reflectionCount && hit; i++)
                    {
                        rayDirection = normalize(reflect(rayDirection, normal));
                        rayOrigin = hitPosition + rayDirection * 0.01;
                        hit = raymarching(rayOrigin, rayDirection, maxDistance, maxDistance * mipLevel, maxIterations / mipLevel, hitPosition, dColor);

                        if (hit)
                        {
                            normal = getNormal(hitPosition);
                            s = shading(hitPosition, normal, dColor);
                            result += fixed4(s * reflectionIntensity, 0);
                        }

                        mipLevel *= 2;
                    }

                    if (reflectionCount > 0)
                        result += fixed4(texCUBE(reflectionCube, rayDirection).rgb * envReflIntensity * reflectionIntensity, 0);
                }
                else
                    result = fixed4(0, 0, 0, 0);                

                return fixed4(color * (1 - result.w) + result.xyz * result.w,1.0);
            }
            ENDCG
        }
    }
}
