// The light behind a lifted run (D-061).
//
// Alpha blending cannot make a shape glow: it interpolates towards the sprite's colour, so a bright
// tint over the purple board reads as a pale patch. Additive blending *adds* light to what is already
// drawn, which is what a glow is — and it costs no post-processing, so the Game camera keeps its
// render path (and a phone keeps the frame time bloom would take).
//
// The smallest shader that can do it: unlit, additive, texture times a tint this shader owns. The tint
// is *not* taken from the SpriteRenderer's colour — neither the vertex colour nor `_RendererColor`
// carries it here, which is what drew every glow white (D-065) — it is `_Tint`, set per renderer
// through a property block, so one material still lights twelve brick colours without cloning.
Shader "Colorful Sort/Block Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.5

        // Two kinds of light are drawn with this shader and they want opposite answers here. The glow
        // sits *behind* a lifted run and is meant to be occluded — the depth test is what leaves only
        // its rim showing. A spark is in *front* of the board and must never be occluded, and sharing
        // the glow's material is what made every burst invisible: they were emitted inside a brick's
        // own mesh and correctly hidden by it (D-079). LessEqual (4) is the default, so a material
        // saved before this property existed keeps exactly the behaviour it had.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            // Add to the frame, read depth but never write it: the glow sits behind opaque bricks, so
            // the depth test is what hides everything but the rim, and writing depth would have it
            // occlude whatever is drawn after. Stated inside the pass, which is where URP expects
            // render state.
            Blend One One
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 colour : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 colour : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // The tint is a property of *this* shader, set per renderer through a property block.
            //
            // Neither of the routes Unity's own sprite shaders use worked here: the vertex colour
            // arrives white, and declaring `_RendererColor` did not get it filled either — measured,
            // not assumed, with a diagnostic that printed the tint the renderer was given (a correct
            // light pink) next to a screen that was still white (D-065). A property we own cannot be
            // taken away by a batching path, and a property block sets it without cloning the
            // material, so one material still lights every colour.
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Intensity;

                // Declared and never read. It is render state, resolved when the material is bound —
                // but the SRP batcher requires *every* non-texture property to sit in this buffer, and
                // a shader it rejects takes a different path through batching. That path is what D-064
                // and D-065 spent three rounds on, so the compatibility is not worth losing over a
                // property this pass does not sample.
                float _ZTest;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.colour = input.colour;
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Vertex colour is still multiplied in: it is white on the batched path, so it costs
                // nothing, and on any path that *does* carry a colour it stays honest.
                half4 tint = input.colour * _Tint;

                // The sprite carries the shape in its alpha — this drawing is white with the tray's
                // own soft edge — so alpha says how much light lands here and the tint says its hue.
                half3 light = texel.rgb * tint.rgb * (texel.a * tint.a * _Intensity);

                // Deliberately *not* clamped. The ratio between the channels is the colour, and
                // scaling the triple back to a peak of 1 — which this used to do — is what kept the
                // glow honest but dim: nothing ever passed bloom's threshold, so nothing could read as
                // light (D-066). The pipeline renders to an HDR target, so a value above 1 survives to
                // the bloom pass and comes back as brightness instead of being clipped to white.
                //
                // The cap belongs back here the day HDR is turned off, and the symptom will be the
                // white wash this series has already chased twice.
                return half4(light, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
