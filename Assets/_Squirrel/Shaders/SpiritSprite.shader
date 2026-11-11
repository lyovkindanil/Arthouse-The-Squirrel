// Белка-дух: спрайт на меше (меш колышется кодом), окраска и прозрачность — цветом вершин.
// Обычный unlit без LightMode: рисуется и в URP 2D, и во встроенном рендере.
Shader "Squirrel/SpiritSprite"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Glow ("Glow (подсветка ярких мест)", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _Glow;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb *= i.color.rgb;
                // Светлый мех чуть «пересвечен» — без постобработки это и есть лёгкое свечение.
                c.rgb += c.rgb * c.rgb * _Glow;
                c.a *= i.color.a;
                return c;
            }
            ENDCG
        }
    }
}
