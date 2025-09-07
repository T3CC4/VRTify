using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRTify.Core;
using VRTify.Data;

namespace VRTify.Editor.Generators
{
    /// <summary>
    /// Sets up VRTify notification HUD on VRChat avatars
    /// </summary>
    public class VRTifyAvatarSetup
    {
        /// <summary>
        /// HUD position presets
        /// </summary>
        public enum HUDPosition
        {
            TopLeft,
            TopCenter,
            TopRight,
            MiddleLeft,
            MiddleCenter,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

        /// <summary>
        /// Creates complete VRTify HUD setup on avatar
        /// </summary>
        public static GameObject SetupVRTifyOnAvatar(VRCAvatarDescriptor avatar, List<NotificationApp> apps, VRTifySettings settings, HUDPosition hudPosition = HUDPosition.BottomCenter)
        {
            try
            {
                // 1. Create HUD GameObject as child of head
                var hudObject = CreateHUDObject(avatar, hudPosition);

                // 2. Generate icon atlas
                var iconAtlas = GenerateIconAtlas(apps, (int)settings.iconSize);

                // 3. Generate dynamic HUD shader
                var hudShader = GenerateHUDShader(apps.Count, settings);

                // 4. Create and setup material
                var hudMaterial = CreateHUDMaterial(hudShader, iconAtlas, apps, settings);

                // 5. Setup mesh renderer
                SetupMeshRenderer(hudObject, hudMaterial);

                // 6. Setup animator controller
                SetupAnimatorController(avatar, apps, settings);

                // 7. Setup expression parameters
                SetupExpressionParameters(avatar, apps);

                Debug.Log($"✅ VRTify HUD setup complete on {avatar.name}");
                return hudObject;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ VRTify setup failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates HUD GameObject attached to avatar head
        /// </summary>
        private static GameObject CreateHUDObject(VRCAvatarDescriptor avatar, HUDPosition position)
        {
            // Find head bone
            var headBone = avatar.transform.Find("Armature/Hips/Spine/Chest/Neck/Head") ??
                          avatar.transform.Find("Head") ??
                          FindBoneRecursive(avatar.transform, "head");

            if (headBone == null)
            {
                Debug.LogWarning("Head bone not found, using avatar root");
                headBone = avatar.transform;
            }

            // Create HUD container
            var hudContainer = new GameObject("VRTify_NotificationHUD");
            hudContainer.transform.SetParent(headBone);

            // Position based on preset
            var localPos = GetHUDLocalPosition(position);
            hudContainer.transform.localPosition = localPos;
            hudContainer.transform.localRotation = Quaternion.identity;
            hudContainer.transform.localScale = Vector3.one;

            // Create quad mesh for HUD
            var hudQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hudQuad.name = "VRTify_HUD_Quad";
            hudQuad.transform.SetParent(hudContainer.transform);
            hudQuad.transform.localPosition = Vector3.zero;
            hudQuad.transform.localRotation = Quaternion.identity;
            hudQuad.transform.localScale = new Vector3(0.4f, 0.1f, 1f); // HUD size

            // Remove collider (not needed for HUD)
            var collider = hudQuad.GetComponent<Collider>();
            if (collider) Object.DestroyImmediate(collider);

            return hudQuad;
        }

        /// <summary>
        /// Gets local position for HUD based on position preset
        /// </summary>
        private static Vector3 GetHUDLocalPosition(HUDPosition position)
        {
            // Positions relative to head bone in viewspace
            return position switch
            {
                HUDPosition.TopLeft => new Vector3(-0.15f, 0.25f, 0.3f),
                HUDPosition.TopCenter => new Vector3(0f, 0.25f, 0.3f),
                HUDPosition.TopRight => new Vector3(0.15f, 0.25f, 0.3f),
                HUDPosition.MiddleLeft => new Vector3(-0.2f, 0f, 0.3f),
                HUDPosition.MiddleCenter => new Vector3(0f, 0f, 0.3f),
                HUDPosition.MiddleRight => new Vector3(0.2f, 0f, 0.3f),
                HUDPosition.BottomLeft => new Vector3(-0.15f, -0.2f, 0.3f),
                HUDPosition.BottomCenter => new Vector3(0f, -0.2f, 0.3f),
                HUDPosition.BottomRight => new Vector3(0.15f, -0.2f, 0.3f),
                _ => new Vector3(0f, -0.2f, 0.3f)
            };
        }

        /// <summary>
        /// Finds bone recursively by name
        /// </summary>
        private static Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name.ToLower().Contains(boneName.ToLower()))
                return parent;

            foreach (Transform child in parent)
            {
                var result = FindBoneRecursive(child, boneName);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Generates icon atlas with proper UV mapping
        /// </summary>
        private static Texture2D GenerateIconAtlas(List<NotificationApp> apps, int iconSize)
        {
            var validApps = apps.Where(a => !string.IsNullOrEmpty(a.iconPath)).ToList();
            if (validApps.Count == 0) return Texture2D.whiteTexture;

            // Calculate atlas size
            var iconsPerRow = Mathf.CeilToInt(Mathf.Sqrt(validApps.Count));
            var atlasSize = Mathf.NextPowerOfTwo(iconsPerRow * iconSize);

            var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
            var clearPixels = new Color[atlasSize * atlasSize];
            System.Array.Fill(clearPixels, Color.clear);
            atlas.SetPixels(clearPixels);

            // Place icons in atlas
            for (int i = 0; i < validApps.Count; i++)
            {
                var iconTexture = ExtractIconTexture(validApps[i].iconPath);
                if (iconTexture != null)
                {
                    var resized = ResizeTexture(iconTexture, iconSize, iconSize);

                    var x = (i % iconsPerRow) * iconSize;
                    var y = (i / iconsPerRow) * iconSize;

                    atlas.SetPixels(x, y, iconSize, iconSize, resized.GetPixels());

                    // Store UV coordinates in app data
                    validApps[i].hudPosition = new Vector2(
                        (float)x / atlasSize,
                        (float)y / atlasSize
                    );

                    Object.DestroyImmediate(resized);
                    Object.DestroyImmediate(iconTexture);
                }
            }

            atlas.Apply();

            // Save atlas
            var atlasPath = VRTifyFileManager.GetUniqueFileName(
                Path.Combine(VRTifyFileManager.TEXTURES_PATH, "Generated"),
                "VRTify_IconAtlas",
                ".png"
            );

            var pngBytes = atlas.EncodeToPNG();
            VRTifyFileManager.WriteBytesToFile(atlasPath, pngBytes);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(VRTifyFileManager.GetRelativeUnityPath(atlasPath));
        }

        /// <summary>
        /// Generates HUD shader with dynamic icon slots
        /// </summary>
        private static Shader GenerateHUDShader(int maxIcons, VRTifySettings settings)
        {
            var shaderCode = GenerateDynamicHUDShaderCode(maxIcons, settings);
            var shaderPath = VRTifyFileManager.GetUniqueFileName(
                Path.Combine(VRTifyFileManager.SHADERS_PATH, "Generated"),
                "VRTify_DynamicHUD",
                ".shader"
            );

            VRTifyFileManager.WriteTextFile(shaderPath, shaderCode);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<Shader>(VRTifyFileManager.GetRelativeUnityPath(shaderPath));
        }

        /// <summary>
        /// Generates shader code with dynamic icon positioning
        /// </summary>
        private static string GenerateDynamicHUDShaderCode(int maxIcons, VRTifySettings settings)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Shader \"VRTify/DynamicNotificationHUD\"");
            sb.AppendLine("{");
            sb.AppendLine("    Properties");
            sb.AppendLine("    {");
            sb.AppendLine("        _MainTex (\"Icon Atlas\", 2D) = \"white\" {}");
            sb.AppendLine("        _IconSize (\"Icon Size\", Float) = 0.1");
            sb.AppendLine("        _IconSpacing (\"Icon Spacing\", Float) = 0.02");
            sb.AppendLine("        _HUDOpacity (\"HUD Opacity\", Range(0,1)) = 0.9");
            sb.AppendLine("        _ActiveIconCount (\"Active Icon Count\", Int) = 0");
            sb.AppendLine();

            // Icon properties
            for (int i = 0; i < maxIcons; i++)
            {
                sb.AppendLine($"        _Icon{i}Active (\"Icon {i} Active\", Float) = 0");
                sb.AppendLine($"        _Icon{i}UV (\"Icon {i} UV\", Vector) = (0,0,0.1,0.1)");
                sb.AppendLine($"        _Icon{i}Alpha (\"Icon {i} Alpha\", Range(0,1)) = 0");
                sb.AppendLine($"        _Icon{i}Scale (\"Icon {i} Scale\", Range(0,2)) = 1");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    SubShader");
            sb.AppendLine("    {");
            sb.AppendLine("        Tags { \"RenderType\"=\"Transparent\" \"Queue\"=\"Overlay+100\" }");
            sb.AppendLine("        Blend SrcAlpha OneMinusSrcAlpha");
            sb.AppendLine("        ZWrite Off");
            sb.AppendLine("        ZTest Always");
            sb.AppendLine("        Cull Off");
            sb.AppendLine();
            sb.AppendLine("        Pass");
            sb.AppendLine("        {");
            sb.AppendLine("            CGPROGRAM");
            sb.AppendLine("            #pragma vertex vert");
            sb.AppendLine("            #pragma fragment frag");
            sb.AppendLine("            #include \"UnityCG.cginc\"");
            sb.AppendLine();

            // Variables
            sb.AppendLine("            sampler2D _MainTex;");
            sb.AppendLine("            float _IconSize, _IconSpacing, _HUDOpacity;");
            sb.AppendLine("            int _ActiveIconCount;");

            for (int i = 0; i < maxIcons; i++)
            {
                sb.AppendLine($"            float _Icon{i}Active, _Icon{i}Alpha, _Icon{i}Scale;");
                sb.AppendLine($"            float4 _Icon{i}UV;");
            }

            sb.AppendLine();
            sb.AppendLine("            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };");
            sb.AppendLine("            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };");
            sb.AppendLine();
            sb.AppendLine("            v2f vert (appdata v)");
            sb.AppendLine("            {");
            sb.AppendLine("                v2f o;");
            sb.AppendLine("                o.vertex = UnityObjectToClipPos(v.vertex);");
            sb.AppendLine("                o.uv = v.uv;");
            sb.AppendLine("                return o;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            fixed4 frag (v2f i) : SV_Target");
            sb.AppendLine("            {");
            sb.AppendLine("                fixed4 finalColor = fixed4(0,0,0,0);");
            sb.AppendLine();
            sb.AppendLine("                // Calculate dynamic positioning");
            sb.AppendLine("                float totalWidth = _ActiveIconCount * _IconSize + (_ActiveIconCount - 1) * _IconSpacing;");
            sb.AppendLine("                float startX = 0.5 - totalWidth * 0.5;");
            sb.AppendLine();
            sb.AppendLine("                int activeIndex = 0;");
            sb.AppendLine();

            // Dynamic icon positioning
            for (int i = 0; i < maxIcons; i++)
            {
                sb.AppendLine($"                if (_Icon{i}Active > 0.5)");
                sb.AppendLine("                {");
                sb.AppendLine("                    float iconX = startX + activeIndex * (_IconSize + _IconSpacing);");
                sb.AppendLine("                    float iconY = 0.5 - _IconSize * 0.5;");
                sb.AppendLine();
                sb.AppendLine("                    if (i.uv.x >= iconX && i.uv.x <= iconX + _IconSize &&");
                sb.AppendLine("                        i.uv.y >= iconY && i.uv.y <= iconY + _IconSize)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        float2 localUV = (i.uv - float2(iconX, iconY)) / _IconSize;");
                sb.AppendLine($"                        float2 atlasUV = _Icon{i}UV.xy + localUV * _Icon{i}UV.zw;");
                sb.AppendLine("                        fixed4 iconColor = tex2D(_MainTex, atlasUV);");
                sb.AppendLine($"                        iconColor.a *= _Icon{i}Alpha * _HUDOpacity;");
                sb.AppendLine("                        finalColor = lerp(finalColor, iconColor, iconColor.a);");
                sb.AppendLine("                    }");
                sb.AppendLine("                    activeIndex++;");
                sb.AppendLine("                }");
                sb.AppendLine();
            }

            sb.AppendLine("                return finalColor;");
            sb.AppendLine("            }");
            sb.AppendLine("            ENDCG");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Creates and configures HUD material
        /// </summary>
        private static Material CreateHUDMaterial(Shader shader, Texture2D iconAtlas, List<NotificationApp> apps, VRTifySettings settings)
        {
            var material = new Material(shader);
            material.name = "VRTify_HUD_Material";

            // Set main texture
            material.SetTexture("_MainTex", iconAtlas);

            // Set HUD properties
            material.SetFloat("_IconSize", settings.iconSize / 1000f); // Convert to 0-1 range
            material.SetFloat("_IconSpacing", settings.iconSpacing / 1000f);
            material.SetFloat("_HUDOpacity", settings.hudOpacity);

            // Set icon UV coordinates
            var validApps = apps.Where(a => !string.IsNullOrEmpty(a.iconPath)).ToList();
            for (int i = 0; i < validApps.Count; i++)
            {
                var app = validApps[i];
                var iconSize = settings.iconSize / (float)iconAtlas.width;
                material.SetVector($"_Icon{i}UV", new Vector4(
                    app.hudPosition.x,
                    app.hudPosition.y,
                    iconSize,
                    iconSize
                ));
            }

            // Save material
            var materialPath = VRTifyFileManager.GetUniqueFileName(
                Path.Combine(VRTifyFileManager.MATERIALS_PATH, "Generated"),
                "VRTify_HUD_Material",
                ".mat"
            );

            AssetDatabase.CreateAsset(material, VRTifyFileManager.GetRelativeUnityPath(materialPath));
            AssetDatabase.SaveAssets();

            return material;
        }

        /// <summary>
        /// Sets up mesh renderer on HUD object
        /// </summary>
        private static void SetupMeshRenderer(GameObject hudObject, Material material)
        {
            var renderer = hudObject.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = hudObject.AddComponent<MeshRenderer>();

            renderer.material = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Sets up animator controller with OSC parameters
        /// </summary>
        private static void SetupAnimatorController(VRCAvatarDescriptor avatar, List<NotificationApp> apps, VRTifySettings settings)
        {
            // This will be implemented in the next file - VRTifyAnimatorSetup.cs
            var animatorSetup = new VRTifyAnimatorSetup();
            animatorSetup.SetupAnimator(avatar, apps, settings);
        }

        /// <summary>
        /// Sets up expression parameters for OSC
        /// </summary>
        private static void SetupExpressionParameters(VRCAvatarDescriptor avatar, List<NotificationApp> apps)
        {
            // This will be implemented in the next file - VRTifyAnimatorSetup.cs
            var animatorSetup = new VRTifyAnimatorSetup();
            animatorSetup.SetupExpressionParameters(avatar, apps);
        }

        // Helper methods
        private static Texture2D ExtractIconTexture(string iconPath)
        {
            // Implementation from VRTifyIconExtractor
            var iconExtractor = new VRTify.Editor.Icons.VRTifyIconExtractor();
            return iconExtractor.ExtractIconAsTexture(iconPath);
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            // Implementation from VRTifyIconExtractor 
            var iconExtractor = new VRTify.Editor.Icons.VRTifyIconExtractor();
            return iconExtractor.ResizeTexture(source, width, height);
        }
    }
}