Shader "Custom/GeometricEdgeOutline" {
    Properties {
        [HDR] _OutlineColor ("Outline Color", Color) = (0,1,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }
    SubShader {
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2g {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            struct g2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            float _OutlineWidth;
            fixed4 _OutlineColor;

            v2g vert (appdata v) {
                v2g o;
                o.vertex = v.vertex;
                o.normal = v.normal;
                return o;
            }

            [maxvertexcount(6)]
            void geom(triangle v2g input[3], inout LineStream<g2f> lineStream) {
                // Create outline for each edge
                for(int i=0; i<3; i++) {
                    int next = (i+1)%3;
                    
                    // Calculate edge vector
                    float3 edgeVec = input[next].vertex - input[i].vertex;
                    float3 faceNormal = normalize(cross(edgeVec, input[i].normal));
                    
                    // Calculate extrusion direction
                    float3 outlineDir = normalize(cross(faceNormal, edgeVec)) * _OutlineWidth;
                    
                    // Create extruded line geometry
                    g2f p0, p1;
                    p0.pos = UnityObjectToClipPos(input[i].vertex + outlineDir);
                    p1.pos = UnityObjectToClipPos(input[next].vertex + outlineDir);
                    p0.color = _OutlineColor;
                    p1.color = _OutlineColor;
                    
                    lineStream.Append(p0);
                    lineStream.Append(p1);
                    lineStream.RestartStrip();
                }
            }

            fixed4 frag (g2f i) : SV_Target {
                return i.color;
            }
            ENDCG
        }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                return float4(0,0,0,0);
            }
            ENDCG
        }
    }
}
