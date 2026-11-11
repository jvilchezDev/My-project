using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Mars;

namespace Mars.Tools
{
    /// <summary>
    /// Builds the MarsPrototype scene from code: procedural Mars textures (albedo, height, normal,
    /// starfield), a high-res sphere mesh, URP materials, atmosphere rim, Sun, skybox, Volume, camera and moons.
    /// Run from the menu: Mars > Build MarsPrototype Scene.
    /// </summary>
    public static class MarsPrototypeBuilder
    {
        const string Root = "Assets/Mars";
        const string TexDir = Root + "/Textures";
        const string MatDir = Root + "/Materials";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/MarsPrototype.unity";
        const int W = 2048;
        const int H = 1024;

        // Crater data shared by the height/albedo generator.
        static Vector3[] craterCenters;
        static float[] craterRadius;
        static float[] craterDepth;

        [MenuItem("Mars/Build MarsPrototype Scene")]
        public static void Build()
        {
            try
            {
                string albedoPath = TexDir + "/Mars_Albedo.png";
                string heightPath = TexDir + "/Mars_Height.png";
                string normalPath = TexDir + "/Mars_Normal.png";
                string starsPath = TexDir + "/Stars_Equirect.png";

                EditorUtility.DisplayProgressBar("Mars", "Generating procedural Mars textures...", 0.05f);
                GenerateTextures(albedoPath, heightPath, normalPath, starsPath);

                EditorUtility.DisplayProgressBar("Mars", "Creating sphere mesh...", 0.5f);
                Mesh sphere = CreateSphereMesh(SceneDir + "/MarsSphere.asset", 128, 256);

                EditorUtility.DisplayProgressBar("Mars", "Creating materials...", 0.6f);
                Material surface = CreateSurfaceMaterial(MatDir + "/Mars_Surface.mat",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath));
                Material atmosphere = CreateAtmosphereMaterial(MatDir + "/Mars_Atmosphere.mat");
                Material sky = CreateSkyMaterial(MatDir + "/Mars_SpaceSkybox.mat",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(starsPath));
                Material moon = CreateSurfaceMaterial(MatDir + "/Moon_Gray.mat", null, null, null);
                moon.SetColor("_BaseColor", new Color(0.5f, 0.46f, 0.42f));
                moon.SetFloat("_Smoothness", 0.05f);
                EditorUtility.SetDirty(moon);

                EditorUtility.DisplayProgressBar("Mars", "Creating post-processing profile...", 0.7f);
                VolumeProfile profile = CreateVolumeProfile(SceneDir + "/Mars_PostProcessing.asset");

                EditorUtility.DisplayProgressBar("Mars", "Assembling scene...", 0.8f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Camera
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 500f;
                var camData = camGo.GetComponent<UniversalAdditionalCameraData>() ??
                              camGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
                camGo.transform.position = new Vector3(0f, 1.5f, -7f);

                // Sun
                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.88f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // Planet
                var marsGo = new GameObject("Mars");
                marsGo.transform.localScale = Vector3.one * 2f; // mesh radius 1 -> world radius 2
                marsGo.AddComponent<MeshFilter>().sharedMesh = sphere;
                var mr = marsGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = surface;
                mr.receiveShadows = true;
                marsGo.AddComponent<PlanetRotator>();

                // Atmosphere shell
                var atmoGo = new GameObject("Atmosphere");
                atmoGo.transform.SetParent(marsGo.transform.parent, false);
                atmoGo.transform.localScale = Vector3.one * 2.12f;
                atmoGo.AddComponent<MeshFilter>().sharedMesh = sphere;
                var amr = atmoGo.AddComponent<MeshRenderer>();
                amr.sharedMaterial = atmosphere;
                amr.shadowCastingMode = ShadowCastingMode.Off;
                amr.receiveShadows = false;

                // Moons (exaggerated distances for readability)
                var phobos = CreateMoon("Phobos", 0.18f, moon, marsGo.transform, 3.6f, 14f, 1.8f);
                var deimos = CreateMoon("Deimos", 0.12f, moon, marsGo.transform, 5.0f, 33f, 0.9f);

                // Orbit camera target
                var orbit = camGo.AddComponent<Mars.OrbitCamera>();
                SetRef(orbit, "target", marsGo.transform);
                SetFloat(orbit, "distance", 7f);

                // Global post-processing volume
                var volGo = new GameObject("Post Processing Volume");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;

                // Skybox
                RenderSettings.skybox = sky;

                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Mars] MarsPrototype scene built at " + ScenePath + " (moons: " + phobos.name + ", " + deimos.name + ")");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static GameObject CreateMoon(string name, float scale, Material mat, Transform center, float radius, float period, float inclination)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var orbit = go.AddComponent<MoonOrbit>();
            SetRef(orbit, "center", center);
            SetFloat(orbit, "radius", radius);
            SetFloat(orbit, "period", period);
            SetFloat(orbit, "inclinationDegrees", inclination);
            return go;
        }

        static void SetRef(Object target, string prop, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(prop).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string prop, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(prop).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- textures

        static void GenerateTextures(string albedoPath, string heightPath, string normalPath, string starsPath)
        {
            GenerateCraters(140, 1234);

            float[] heights = new float[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    heights[y * W + x] = Mathf.Clamp01(Height(DirFromPixel(x, y)));
                }
                if (y % 64 == 0) EditorUtility.DisplayProgressBar("Mars", "Height map...", 0.1f + 0.1f * y / H);
            }

            var albedo = new Color32[W * H];
            var heightPx = new Color32[W * H];
            var normalPx = new Color32[W * H];
            const float normalStrength = 60f;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    Vector3 d = DirFromPixel(x, y);
                    float hv = heights[i];

                    albedo[i] = ToColor32(Albedo(d, hv));

                    byte hb = (byte)Mathf.RoundToInt(hv * 255f);
                    heightPx[i] = new Color32(hb, hb, hb, 255);

                    float hL = heights[y * W + ((x - 1 + W) % W)];
                    float hR = heights[y * W + ((x + 1) % W)];
                    float hD = heights[Mathf.Max(0, y - 1) * W + x];
                    float hU = heights[Mathf.Min(H - 1, y + 1) * W + x];
                    Vector3 n = new Vector3(-(hR - hL) * normalStrength, -(hU - hD) * normalStrength, 1f).normalized;
                    normalPx[i] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                }
                if (y % 64 == 0) EditorUtility.DisplayProgressBar("Mars", "Albedo and normal...", 0.2f + 0.2f * y / H);
            }

            WritePng(albedoPath, albedo);
            WritePng(heightPath, heightPx);
            WritePng(normalPath, normalPx);
            ConfigureTexture(albedoPath, TextureImporterType.Default, true);
            ConfigureTexture(heightPath, TextureImporterType.Default, false);
            ConfigureTexture(normalPath, TextureImporterType.NormalMap, false);

            WritePng(starsPath, GenerateStars());
            ConfigureTexture(starsPath, TextureImporterType.Default, true);
        }

        // Mathf.SmoothStep returns a value between from/to, so use a 0..1 smoothstep for blend factors.
        static float Smooth01(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static Color Albedo(Vector3 d, float hv)
        {
            Color rust = new Color(0.55f, 0.23f, 0.12f);
            Color ochre = new Color(0.83f, 0.48f, 0.27f);
            Color dark = new Color(0.30f, 0.17f, 0.12f);
            Color dust = new Color(0.92f, 0.64f, 0.42f);

            float region = Fbm(d * 1.7f + new Vector3(10f, 0f, 0f), 4);
            Color c = Color.Lerp(rust, ochre, Smooth01(0.35f, 0.65f, region));
            float darkMask = Smooth01(0.55f, 0.75f, Fbm(d * 3.5f + new Vector3(5f, 2f, 0f), 4));
            c = Color.Lerp(c, dark, darkMask * 0.6f);
            c = Color.Lerp(c, dust, Fbm(d * 6f, 3) * 0.2f);
            c *= 0.82f + 0.30f * hv; // craters/low areas a bit darker

            // Polar caps
            float ay = Mathf.Abs(d.y) + (Fbm(d * 8f, 3) - 0.5f) * 0.08f;
            float cap = Smooth01(0.86f, 0.93f, ay);
            c = Color.Lerp(c, new Color(0.93f, 0.94f, 0.97f), cap);

            c.a = 1f;
            return c;
        }

        static float Height(Vector3 d)
        {
            float v = Fbm(d * 2.5f, 5) * 0.6f + Fbm(d * 7f, 4) * 0.25f;
            for (int i = 0; i < craterCenters.Length; i++)
            {
                float t = (d - craterCenters[i]).magnitude / craterRadius[i];
                if (t < 1f)
                {
                    v -= craterDepth[i] * (1f - t * t);
                }
                else
                {
                    float q = (t - 1.08f) / 0.17f;
                    if (q > -1f && q < 1f) v += craterDepth[i] * 0.35f * (1f - q * q);
                }
            }
            return v;
        }

        static void GenerateCraters(int count, int seed)
        {
            var rng = new System.Random(seed);
            craterCenters = new Vector3[count];
            craterRadius = new float[count];
            craterDepth = new float[count];
            for (int i = 0; i < count; i++)
            {
                craterCenters[i] = RandomOnSphere(rng);
                float r = Mathf.Lerp(0.02f, 0.14f, Mathf.Pow((float)rng.NextDouble(), 3f));
                craterRadius[i] = r;
                craterDepth[i] = r * 0.5f;
            }
        }

        static Vector3 RandomOnSphere(System.Random rng)
        {
            double u = rng.NextDouble() * 2.0 - 1.0;
            double t = rng.NextDouble() * Mathf.PI * 2.0;
            double s = System.Math.Sqrt(1.0 - u * u);
            return new Vector3((float)(s * System.Math.Cos(t)), (float)u, (float)(s * System.Math.Sin(t)));
        }

        static Color32[] GenerateStars()
        {
            var px = new Color[W * H];
            var rng = new System.Random(99);
            for (int s = 0; s < 9000; s++)
            {
                Vector3 d = RandomOnSphere(rng);
                float lon = Mathf.Atan2(d.z, d.x);
                float lat = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f));
                int x = Mathf.Clamp((int)((lon / (Mathf.PI * 2f) + 0.5f) * W), 0, W - 1);
                int y = Mathf.Clamp((int)((lat / Mathf.PI + 0.5f) * H), 0, H - 1);
                float b = Mathf.Pow((float)rng.NextDouble(), 10f) * 1.6f + 0.15f;
                float tint = (float)rng.NextDouble();
                Color c = tint < 0.5f
                    ? new Color(0.8f, 0.88f, 1f)  // bluish
                    : new Color(1f, 0.94f, 0.82f); // warm
                AddPixel(px, x, y, c * b);
                AddPixel(px, x + 1, y, c * b * 0.25f);
                AddPixel(px, x - 1, y, c * b * 0.25f);
            }

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Vector3 d = DirFromPixel(x, y);
                    float band = Mathf.Exp(-(d.y * d.y) / 0.02f) * (0.03f + 0.08f * Fbm(d * 4f, 4));
                    px[y * W + x] += new Color(band * 0.9f, band * 0.8f, band * 0.7f);
                }
            }

            var result = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                px[i].a = 1f;
                result[i] = ToColor32(px[i]);
            }
            return result;
        }

        static void AddPixel(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[y * W + x] += c;
        }

        static Vector3 DirFromPixel(int x, int y)
        {
            float lon = ((float)x / W) * Mathf.PI * 2f;
            float lat = ((float)y / H) * Mathf.PI - Mathf.PI * 0.5f;
            float cl = Mathf.Cos(lat);
            return new Vector3(cl * Mathf.Cos(lon), Mathf.Sin(lat), cl * Mathf.Sin(lon));
        }

        static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                255);
        }

        // 3D value noise, so the texture has no seam at the equirectangular wrap.
        static float Hash3(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        static float ValueNoise(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);

            float c000 = Hash3(xi, yi, zi), c100 = Hash3(xi + 1, yi, zi);
            float c010 = Hash3(xi, yi + 1, zi), c110 = Hash3(xi + 1, yi + 1, zi);
            float c001 = Hash3(xi, yi, zi + 1), c101 = Hash3(xi + 1, yi, zi + 1);
            float c011 = Hash3(xi, yi + 1, zi + 1), c111 = Hash3(xi + 1, yi + 1, zi + 1);

            float x00 = Mathf.Lerp(c000, c100, fx), x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx), x11 = Mathf.Lerp(c011, c111, fx);
            float y0 = Mathf.Lerp(x00, x10, fy), y1 = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0, y1, fz);
        }

        static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * ValueNoise(p);
                norm += amp;
                amp *= 0.5f;
                p *= 2.03f;
            }
            return sum / norm;
        }

        static void WritePng(string path, Color32[] pixels)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool srgb)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = type;
            imp.sRGBTexture = srgb;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        // ---------------------------------------------------------------- mesh

        static Mesh CreateSphereMesh(string path, int lat, int lon)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float v = (float)y / lat;
                float phi = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, v);
                for (int x = 0; x <= lon; x++)
                {
                    float u = (float)x / lon;
                    float th = u * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th));
                    verts.Add(d);
                    uvs.Add(new Vector2(u, v));
                }
            }
            int row = lon + 1;
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * row + x, b = a + row;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }

            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "MarsSphere", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ---------------------------------------------------------------- materials

        static Material NewAsset(string path, Shader shader)
        {
            if (shader == null) throw new System.Exception("Shader not found for " + path);
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material CreateSurfaceMaterial(string path, Texture2D albedo, Texture2D normal, Texture2D height)
        {
            var m = NewAsset(path, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.15f);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (height != null)
            {
                // URP Lit's height input is parallax occlusion-style, not vertex displacement.
                m.SetTexture("_ParallaxMap", height);
                m.SetFloat("_Parallax", 0.02f);
                m.EnableKeyword("_PARALLAXMAP");
            }
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Material CreateAtmosphereMaterial(string path)
        {
            var m = NewAsset(path, Shader.Find("Mars/Atmosphere Rim"));
            m.SetColor("_RimColor", new Color(1f, 0.55f, 0.42f, 1f));
            m.SetFloat("_Intensity", 1.6f);
            m.SetFloat("_FresnelPower", 4f);
            m.SetFloat("_Opacity", 0.4f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Material CreateSkyMaterial(string path, Texture2D stars)
        {
            var m = NewAsset(path, Shader.Find("Skybox/Panoramic"));
            m.SetTexture("_MainTex", stars);
            m.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        static VolumeProfile CreateVolumeProfile(string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.75f);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
