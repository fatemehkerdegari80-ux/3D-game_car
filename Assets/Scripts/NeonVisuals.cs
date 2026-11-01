using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHighway
{
    public enum CarStyle { Standard, Taxi, Stalled }

    // All meshes and material art are generated locally. No paid/downloaded assets.
    //
    // Art direction ("Midnight Coast"): a deep indigo night, a magenta afterglow on the horizon,
    // wet glossy asphalt, warm sodium lamps and two neon road edges (magenta left, cyan right).
    // Every road segment is baked into a handful of combined meshes (MeshBatch) so the extra detail
    // costs almost nothing at runtime.
    public static class NeonVisuals
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        public static readonly Color Cyan   = new Color(.22f, .86f, .95f);
        public static readonly Color Pink   = new Color(1f, .27f, .58f);
        public static readonly Color Gold   = new Color(1f, .80f, .32f);
        public static readonly Color Mint   = new Color(.36f, .95f, .70f);
        public static readonly Color Violet = new Color(.55f, .42f, 1f);
        public static readonly Color Amber  = new Color(1f, .60f, .22f);
        public static readonly Color PlayerPaint = new Color(.74f, .80f, .90f);
        public static readonly Color FogColor = new Color(.085f, .048f, .150f);

        // ------------------------------------------------------------------ shaders & materials

        // Unity 6 templates commonly use URP. The Built-in "Standard" shader appears
        // magenta in URP; detect the *active pipeline* and select a compatible shader.
        public static Shader FindCompatibleShader(bool transparent = false)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            Shader shader = null;
            if (pipeline != null)
            {
                var pipelineName = pipeline.GetType().FullName ?? string.Empty;
                if (pipelineName.Contains("Universal"))
                {
                    shader = Shader.Find(transparent ?
                        "Universal Render Pipeline/Particles/Unlit" :
                        "Universal Render Pipeline/Lit");
                    if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                else if (pipelineName.Contains("HD"))
                {
                    shader = Shader.Find("HDRP/Lit");
                }
            }
            else
            {
                shader = Shader.Find(transparent ? "Particles/Standard Unlit" : "Standard");
                if (!shader) shader = Shader.Find("Legacy Shaders/Diffuse");
            }
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Hidden/InternalErrorShader");
            return shader;
        }

        public static Material Mat(string name, Color color, bool glow = false, bool transparent = false,
                                   float smooth = -1f, float metal = -1f, float emission = 1.75f)
        {
            Material m;
            if (Cache.TryGetValue(name, out m) && m != null) return m;
            var shader = FindCompatibleShader(transparent);
            if (!shader)
            {
                Debug.LogError("Neon Highway: No compatible shader is available for the active render pipeline.");
                return null;
            }
            // Player builds strip shader variants that are only reached through Shader.Find.
            // Clone a serialized Resources material so URP keeps the Lit and Lit+Emission
            // variants used by this procedural scene in the standalone player.
            Material template = null;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (!transparent && pipeline != null &&
                (pipeline.GetType().FullName ?? string.Empty).Contains("Universal"))
            {
                template = Resources.Load<Material>(glow
                    ? "Materials/NH_LitEmissionTemplate"
                    : "Materials/NH_LitTemplate");
            }
            m = template != null ? new Material(template) : new Material(shader);
            m.name = "NH_" + name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); // URP Lit
            if (m.HasProperty("_Color")) m.SetColor("_Color", color); // Built-in Standard / particles
            if (glow && !transparent)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            float s = smooth >= 0f ? smooth : (glow ? .46f : .65f);
            float mt = metal >= 0f ? metal : (glow ? .05f : .36f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s);   // URP Lit
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", s);   // Built-in Standard
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", mt);
            Cache[name] = m;
            return m;
        }

        static Shader LoadSoftShader(string resourceName, string shaderName)
        {
            var shader = Resources.Load<Shader>("Shaders/" + resourceName);
            if (!shader) shader = Shader.Find(shaderName);
            if (!shader) shader = FindCompatibleShader(true);
            return shader;
        }

        static Material glowMaterial, shadowMaterial;

        // Additive soft-disc material. Vertex colour carries the tint so ONE draw call can hold
        // many differently coloured halos (see GlowBatch).
        public static Material GlowMaterial()
        {
            if (glowMaterial != null) return glowMaterial;
            var shader = LoadSoftShader("SoftParticle", "NeonHighway/SoftParticle");
            glowMaterial = new Material(shader) { name = "NH_additive halo" };
            if (glowMaterial.HasProperty("_Tint")) glowMaterial.SetColor("_Tint", Color.white);
            if (glowMaterial.HasProperty("_BaseColor")) glowMaterial.SetColor("_BaseColor", Color.white);
            if (glowMaterial.HasProperty("_Color")) glowMaterial.SetColor("_Color", Color.white);
            if (glowMaterial.HasProperty("_Brightness")) glowMaterial.SetFloat("_Brightness", 1.7f);
            return glowMaterial;
        }

        static Material ShadowMaterial()
        {
            if (shadowMaterial != null) return shadowMaterial;
            var shader = LoadSoftShader("SoftSmoke", "NeonHighway/SoftSmoke");
            shadowMaterial = new Material(shader) { name = "NH_contact shadow" };
            var dark = new Color(0f, 0f, 0f, .72f);
            if (shadowMaterial.HasProperty("_Tint")) shadowMaterial.SetColor("_Tint", dark);
            if (shadowMaterial.HasProperty("_BaseColor")) shadowMaterial.SetColor("_BaseColor", dark);
            if (shadowMaterial.HasProperty("_Color")) shadowMaterial.SetColor("_Color", dark);
            return shadowMaterial;
        }

        static Material HaloMaterial(Color color, float brightness)
        {
            string key = "halo_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + brightness.ToString("0.00");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(LoadSoftShader("SoftParticle", "NeonHighway/SoftParticle")) { name = "NH_" + key };
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", color);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Brightness")) m.SetFloat("_Brightness", brightness);
            Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ primitive meshes

        static Mesh cubeMesh, quadMesh;

        // Own unit cube (readable, 24 verts) so combined meshes never depend on built-in mesh import flags.
        public static Mesh CubeMesh()
        {
            if (cubeMesh != null) return cubeMesh;
            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in normals)
            {
                Vector3 a = (n == Vector3.up || n == Vector3.down) ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(n, a);
                int s = verts.Count;
                Vector3 c = n * .5f;
                Vector3 p0 = c - a * .5f - b * .5f, p1 = c - a * .5f + b * .5f;
                Vector3 p2 = c + a * .5f + b * .5f, p3 = c + a * .5f - b * .5f;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                for (int i = 0; i < 4; i++) norms.Add(n);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
                // Unity front faces are those where Cross(p1-p0, p2-p0) points at the viewer.
                bool outward = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) > 0f;
                if (outward) { tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); tris.Add(s); tris.Add(s + 2); tris.Add(s + 3); }
                else { tris.Add(s); tris.Add(s + 2); tris.Add(s + 1); tris.Add(s); tris.Add(s + 3); tris.Add(s + 2); }
            }
            cubeMesh = new Mesh { name = "NH unit cube" };
            cubeMesh.SetVertices(verts);
            cubeMesh.SetNormals(norms);
            cubeMesh.SetUVs(0, uvs);
            cubeMesh.SetTriangles(tris, 0);
            cubeMesh.RecalculateBounds();
            return cubeMesh;
        }

        // Unit quad in the XY plane facing -Z (towards the chase camera), UV 0..1, white vertex colour.
        public static Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "NH unit quad" };
            quadMesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        // Collects many boxes and bakes them into one mesh per material.
        public sealed class MeshBatch
        {
            readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();
            public void Add(Material material, Vector3 position, Vector3 size)
            {
                Add(material, position, size, Quaternion.identity);
            }
            public void Add(Material material, Vector3 position, Vector3 size, Quaternion rotation)
            {
                if (material == null) return;
                List<CombineInstance> list;
                if (!parts.TryGetValue(material, out list)) { list = new List<CombineInstance>(); parts[material] = list; }
                var ci = new CombineInstance();
                ci.mesh = CubeMesh();
                ci.transform = Matrix4x4.TRS(position, rotation, size);
                list.Add(ci);
            }
            public void Build(Transform parent)
            {
                foreach (var pair in parts)
                {
                    var go = new GameObject("Batched | " + pair.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, false);
                    var mesh = new Mesh { name = "Combined " + pair.Key.name };
                    if (pair.Value.Count * 24 > 60000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.GetComponent<MeshRenderer>();
                    r.sharedMaterial = pair.Key;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                parts.Clear();
            }
        }

        // Collects soft additive quads (street-lamp halos, light pools on wet asphalt, window glow).
        public sealed class GlowBatch
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Color> cols = new List<Color>();
            readonly List<int> tris = new List<int>();
            public void Add(Vector3 center, Vector3 halfRight, Vector3 halfUp, Color color)
            {
                int i = verts.Count;
                verts.Add(center - halfRight - halfUp); verts.Add(center - halfRight + halfUp);
                verts.Add(center + halfRight + halfUp); verts.Add(center + halfRight - halfUp);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
                for (int k = 0; k < 4; k++) cols.Add(color);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            // Halo that faces the chase camera (XY plane).
            public void Billboard(Vector3 center, float width, float height, Color color)
            {
                Add(center, new Vector3(width * .5f, 0, 0), new Vector3(0, height * .5f, 0), color);
            }
            // Pool of light lying flat on the road (XZ plane).
            public void Flat(Vector3 center, float width, float length, Color color)
            {
                Add(center, new Vector3(width * .5f, 0, 0), new Vector3(0, 0, length * .5f), color);
            }
            // Glow on a building face that looks across the road (ZY plane).
            public void Facade(Vector3 center, float width, float height, Color color)
            {
                Add(center, new Vector3(0, 0, width * .5f), new Vector3(0, height * .5f, 0), color);
            }
            public GameObject Build(Transform parent, string name)
            {
                if (verts.Count == 0) return null;
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                var mesh = new Mesh { name = name };
                if (verts.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(cols);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = GlowMaterial();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear();
                return go;
            }
        }

        // ------------------------------------------------------------------ simple primitives

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            var collider = g.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            return g;
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            var c = g.GetComponent<Collider>(); if (c) Object.Destroy(c);
            return g;
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = rot;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            var c = g.GetComponent<Collider>(); if (c) Object.Destroy(c);
            return g;
        }

        // Single soft halo quad (used by pooled coins / fuel cans). Faces the chase camera.
        public static GameObject Halo(Transform parent, string name, Vector3 localPos, Vector2 size, Color color, float brightness)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = QuadMesh();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = HaloMaterial(color, brightness);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // Soft dark blob under a car: grounds it on the road without real-time shadows.
        public static GameObject ContactShadow(Transform parent, Vector2 size, float y)
        {
            var go = new GameObject("Soft contact shadow", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<MeshFilter>().sharedMesh = QuadMesh();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ShadowMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Material HazardOn()  { return Mat("hazard blinker lit", Amber, true, false, .3f, 0f, 4.2f); }
        public static Material HazardOff() { return Mat("hazard blinker idle", new Color(.30f, .13f, .04f), false, false, .4f, 0f); }

        // ------------------------------------------------------------------ car

        // A faceted but curved GT body assembled from eight cross sections.
        // This is real procedurally generated mesh geometry, not a stack of cubes.
        static GameObject MeshPart(Transform parent, string name, Vector3[] vertices,
                                   int[] triangles, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name + " | generated geometry" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Surface(Transform parent, string name, Material mat,
                                  Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Duplicate back-face vertices so normals do not cancel out.
            // In URP Lit, shared opposite faces can otherwise render black.
            return MeshPart(parent, name, new[] { a, b, c, d, a, b, c, d },
                new[] { 0, 1, 2, 0, 2, 3, 6, 5, 4, 7, 6, 4 }, mat);
        }

        static GameObject GTBody(Transform parent, Material paint)
        {
            float[] z = { -2.20f, -2.02f, -1.48f, -.66f, .38f, 1.26f, 2.01f, 2.24f };
            float[] w = { .78f, .93f, .98f, .99f, .97f, .92f, .85f, .70f };
            float[] h = { .78f, .91f, .99f, 1.02f, .94f, .88f, .73f, .62f };
            const int stride = 6;
            var verts = new Vector3[z.Length * stride + 2];
            var tris = new List<int>(280);
            for (int i = 0; i < z.Length; ++i)
            {
                float wi = w[i], zi = z[i], top = h[i];
                int n = i * stride;
                verts[n + 0] = new Vector3(-wi * .87f, .39f, zi);
                verts[n + 1] = new Vector3( wi * .87f, .39f, zi);
                verts[n + 2] = new Vector3( wi, .67f, zi);
                verts[n + 3] = new Vector3( wi * .82f, top, zi);
                verts[n + 4] = new Vector3(-wi * .82f, top, zi);
                verts[n + 5] = new Vector3(-wi, .67f, zi);
            }
            for (int i = 0; i < z.Length - 1; i++)
                for (int j = 0; j < stride; j++)
                {
                    int a = i * stride + j, b = i * stride + (j + 1) % stride;
                    int c = (i + 1) * stride + j, d = (i + 1) * stride + (j + 1) % stride;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            int rear = verts.Length - 2, front = verts.Length - 1;
            verts[rear] = new Vector3(0, .63f, z[0]);
            verts[front] = new Vector3(0, .58f, z[z.Length - 1]);
            for (int j = 0; j < stride; j++)
            {
                tris.Add(rear); tris.Add((j + 1) % stride); tris.Add(j);
                int k = (z.Length - 1) * stride;
                tris.Add(front); tris.Add(k + j); tris.Add(k + (j + 1) % stride);
            }
            return MeshPart(parent, "Aero chassis", verts, tris.ToArray(), paint);
        }

        public static GameObject CreateCar(Transform parent, Color paint, bool player, string name,
                                           CarStyle style = CarStyle.Standard)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var body = Mat("gt_paint_" + ColorUtility.ToHtmlStringRGB(paint), paint, false, false, .86f, .55f);
            var black = Mat("carbon graphite", new Color(.030f, .036f, .052f), false, false, .55f, .3f);
            var glass = Mat("smoked blue glass", new Color(.035f, .060f, .110f), false, false, .96f, .65f);
            var steel = Mat("brushed gunmetal", new Color(.17f, .21f, .27f), false, false, .75f, .8f);
            var tire = Mat("rubber black", new Color(.018f, .020f, .028f), false, false, .25f, 0f);
            var hub = Mat("machined wheel hubs", new Color(.55f, .60f, .68f), false, false, .85f, .9f);
            var led = Mat("cold-white headlights", new Color(.80f, .92f, .98f), true, false, .5f, 0f, 3.0f);
            var brake = Mat("red LED tail light", new Color(1f, .09f, .13f), true, false, .3f, 0f, 3.6f);
            var sideAccent = Mat(player ? "player GT cyan detail" : "traffic GT graphite detail",
                player ? Cyan : new Color(.35f, .47f, .55f), true, false, .4f, 0f, 2.6f);
            GTBody(root.transform, body);
            // Low, swept cabin: glass panels and a floating roof, not a rectangular cabin.
            Surface(root.transform, "Raked windshield", glass,
                new Vector3(-.66f, 1.38f, .25f), new Vector3(.66f, 1.38f, .25f),
                new Vector3(.81f, .95f, 1.13f), new Vector3(-.81f, .95f, 1.13f));
            Surface(root.transform, "Rear sloping windshield", glass,
                new Vector3(-.63f, 1.35f, -.87f), new Vector3(.63f, 1.35f, -.87f),
                new Vector3(.84f, .98f, -1.47f), new Vector3(-.84f, .98f, -1.47f));
            Surface(root.transform, "Left side panoramic glass", glass,
                new Vector3(-.68f, 1.34f, -.86f), new Vector3(-.68f, 1.34f, .23f),
                new Vector3(-.80f, .96f, 1.09f), new Vector3(-.82f, .98f, -1.40f));
            Surface(root.transform, "Right side panoramic glass", glass,
                new Vector3(.68f, 1.34f, .23f), new Vector3(.68f, 1.34f, -.86f),
                new Vector3(.82f, .98f, -1.40f), new Vector3(.80f, .96f, 1.09f));
            Box(root.transform, "Sculpted floating roof", new Vector3(0, 1.39f, -.32f),
                new Vector3(1.39f, .075f, 1.20f), body);
            Box(root.transform, "Front splitter", new Vector3(0, .41f, 2.23f),
                new Vector3(1.60f, .085f, .29f), black);
            Box(root.transform, "Rear diffuser", new Vector3(0, .45f, -2.20f),
                new Vector3(1.65f, .13f, .24f), black);
            Box(root.transform, "Rear wing upper blade", new Vector3(0, 1.09f, -2.00f),
                new Vector3(1.92f, .075f, .26f), steel);
            Box(root.transform, "Rear LED light bar", new Vector3(0, .79f, -2.17f),
                new Vector3(1.38f, .040f, .05f), brake);
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side;
                Box(root.transform, "Wing support", new Vector3(sx * .63f, .93f, -1.96f),
                    new Vector3(.07f, .26f, .12f), black);
                Box(root.transform, "Aero side skirt", new Vector3(sx * .97f, .43f, -.15f),
                    new Vector3(.095f, .16f, 3.60f), black);
                Box(root.transform, "Side accent pinstripe", new Vector3(sx * .984f, .57f, -.01f),
                    new Vector3(.027f, .025f, 2.34f), sideAccent);
                Box(root.transform, "Side mirror", new Vector3(sx * .94f, 1.03f, .66f),
                    new Vector3(.24f, .13f, .36f), steel);
                Box(root.transform, "Headlamp housing", new Vector3(sx * .64f, .73f, 2.12f),
                    new Vector3(.49f, .18f, .075f), black);
                Box(root.transform, "Thin white LED headlight", new Vector3(sx * .64f, .77f, 2.166f),
                    new Vector3(.39f, .044f, .037f), led);
                Box(root.transform, "Red LED tail light", new Vector3(sx * .62f, .78f, -2.16f),
                    new Vector3(.40f, .072f, .055f), brake);
                Cylinder(root.transform, "Exhaust tip", new Vector3(sx * .46f, .50f, -2.27f),
                    new Vector3(.10f, .07f, .10f), steel, Quaternion.Euler(90, 0, 0));
                // Four wheels with metallic hub insets. Cylinder axis rotated onto X.
                foreach (float wz in new[] { -1.38f, 1.35f })
                {
                    Cylinder(root.transform, "Wide rubber tyre", new Vector3(sx * .96f, .43f, wz),
                        new Vector3(.42f, .15f, .42f), tire, Quaternion.Euler(0, 0, 90));
                    Cylinder(root.transform, "Wheel alloy insert", new Vector3(sx * 1.105f, .43f, wz),
                        new Vector3(.255f, .025f, .255f), hub, Quaternion.Euler(0, 0, 90));
                    Cylinder(root.transform, "Dark hub center", new Vector3(sx * 1.13f, .43f, wz),
                        new Vector3(.085f, .029f, .085f), black, Quaternion.Euler(0, 0, 90));
                }
                if (style == CarStyle.Stalled)
                    Box(root.transform, "Hazard lamp", new Vector3(sx * .86f, .72f, -2.19f),
                        new Vector3(.16f, .13f, .055f), HazardOff());
            }
            Box(root.transform, "GT front grille", new Vector3(0, .57f, 2.24f),
                new Vector3(1.10f, .16f, .058f), black);
            Box(root.transform, "Vent left", new Vector3(-.44f, .92f, 1.46f),
                new Vector3(.25f, .016f, .40f), black);
            Box(root.transform, "Vent right", new Vector3(.44f, .92f, 1.46f),
                new Vector3(.25f, .016f, .40f), black);
            Box(root.transform, "Rear license mount", new Vector3(0, .67f, -2.215f),
                new Vector3(.54f, .16f, .04f), steel);

            var halos = new GlowBatch();
            // Soft tail-light bloom: the single biggest "night driving" cue seen from the chase camera.
            Color red = new Color(1f, .10f, .16f, .85f);
            halos.Billboard(new Vector3(-.62f, .78f, -2.34f), 1.25f, .75f, red);
            halos.Billboard(new Vector3(.62f, .78f, -2.34f), 1.25f, .75f, red);
            halos.Billboard(new Vector3(0, .79f, -2.36f), 2.7f, .50f, new Color(1f, .10f, .16f, .38f));
            if (style == CarStyle.Taxi)
            {
                Box(root.transform, "Taxi roof sign", new Vector3(0, 1.52f, -.30f),
                    new Vector3(.62f, .15f, .30f), Mat("taxi roof lamp", Amber, true, false, .4f, 0f, 3.4f));
                halos.Billboard(new Vector3(0, 1.55f, -.62f), 1.5f, .85f, new Color(1f, .62f, .22f, .75f));
            }
            halos.Build(root.transform, "Light halos");
            // Grounding blob; the player's version is bigger and softer.
            ContactShadow(root.transform, player ? new Vector2(3.2f, 5.8f) : new Vector2(2.9f, 5.3f), -.035f);

            if (player)
                Box(root.transform, "Neon underglow", new Vector3(0, .20f, 0),
                    new Vector3(1.46f, .032f, 2.95f), Mat("underside teal", Cyan, true));
            return root;
        }

        // ------------------------------------------------------------------ road tile

        public static GameObject CreateRoadTile(Transform parent, int id, float length)
        {
            var root = new GameObject("Pooled road segment " + id);
            root.transform.SetParent(parent, false);
            var solid = new MeshBatch();
            var glow = new GlowBatch();
            var rng = new System.Random(11003 + id * 727);

            var asphalt = Mat("wet night asphalt", new Color(.030f, .034f, .052f), false, false, .93f, .12f);
            var patch = Mat("asphalt patch", new Color(.048f, .052f, .074f), false, false, .82f, .08f);
            var paving = Mat("black roadside paving", new Color(.042f, .046f, .066f), false, false, .55f, .10f);
            var rail = Mat("steel guardrail", new Color(.22f, .24f, .31f), false, false, .75f, .65f);
            var ground = Mat("dark graphite ground", new Color(.018f, .020f, .034f), false, false, .12f, 0f);
            var dash = Mat("lane dash", new Color(.95f, .92f, .82f), true, false, .5f, 0f, 1.5f);
            var neonLeft = Mat("edge neon magenta", Pink, true, false, .4f, 0f, 3.2f);
            var neonRight = Mat("edge neon cyan", Cyan, true, false, .4f, 0f, 3.2f);
            var post = Mat("street furniture", new Color(.10f, .12f, .18f), false, false, .55f, .7f);
            var lamp = Mat("sodium street lamps", new Color(1f, .74f, .42f), true, false, .4f, 0f, 3.4f);
            var marker = Mat("small pale markers", new Color(.55f, .85f, .95f), true, false, .5f, 0f, 2.2f);
            var windAmber = Mat("distant warm windows", new Color(1f, .70f, .36f), true, false, .4f, 0f, 1.7f);
            var windCool = Mat("distant cool windows", new Color(.45f, .80f, .95f), true, false, .4f, 0f, 1.7f);
            var windRose = Mat("distant rose windows", new Color(1f, .45f, .70f), true, false, .4f, 0f, 1.6f);
            var beacon = Mat("rooftop red beacon", new Color(1f, .14f, .12f), true, false, .4f, 0f, 3.6f);
            Material[] facades =
            {
                Mat("tower navy",   new Color(.038f, .048f, .092f), false, false, .55f, .25f),
                Mat("tower indigo", new Color(.060f, .044f, .106f), false, false, .55f, .25f),
                Mat("tower slate",  new Color(.048f, .058f, .086f), false, false, .55f, .25f),
                Mat("tower plum",   new Color(.076f, .040f, .088f), false, false, .55f, .25f)
            };
            Material[] signMats =
            {
                Mat("sign neon cyan", Cyan, true, false, .4f, 0f, 3.2f),
                Mat("sign neon pink", Pink, true, false, .4f, 0f, 3.2f),
                Mat("sign neon amber", Amber, true, false, .4f, 0f, 3.2f),
                Mat("sign neon violet", Violet, true, false, .4f, 0f, 3.2f)
            };
            Color[] signColors = { Cyan, Pink, Amber, Violet };

            // --- road surface -------------------------------------------------
            solid.Add(asphalt, new Vector3(0, -.16f, 0), new Vector3(11.6f, .2f, length));
            for (int p = 0; p < 2; p++)
            {
                float px = ((float)rng.NextDouble() * 9.4f) - 4.7f;
                float pz = ((float)rng.NextDouble() - .5f) * (length - 8f);
                solid.Add(patch, new Vector3(px, -.0565f, pz), new Vector3(.9f + (float)rng.NextDouble() * 1.6f, .004f, 4f + (float)rng.NextDouble() * 5f));
            }
            for (int lane = -1; lane <= 1; lane += 2)
            {
                float x = lane * 1.85f;
                for (int j = 0; j < 6; j++)
                    solid.Add(dash, new Vector3(x, -.045f, -length * .5f + 3.5f + j * length / 6f), new Vector3(.10f, .012f, 2.4f));
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float sign = s;
                Color edgeColor = s < 0 ? Pink : Cyan;
                solid.Add(paving, new Vector3(sign * 5.34f, -.11f, 0), new Vector3(.87f, .11f, length));
                solid.Add(s < 0 ? neonLeft : neonRight, new Vector3(sign * 5.68f, -.035f, 0), new Vector3(.09f, .04f, length));
                // light spilling from the neon edge onto the wet asphalt
                for (int j = 0; j < 4; j++)
                    glow.Flat(new Vector3(sign * 5.05f, -.03f, -length * .5f + 5f + j * 10f), 2.7f, 16f,
                        new Color(edgeColor.r, edgeColor.g, edgeColor.b, .30f));
                solid.Add(rail, new Vector3(sign * 6.32f, .42f, 0), new Vector3(.15f, .15f, length));
                for (int j = 0; j < 10; j++)
                    solid.Add(post, new Vector3(sign * 6.32f, .22f, -length * .5f + 2f + j * 4f), new Vector3(.12f, .44f, .12f));
                solid.Add(ground, new Vector3(sign * 29f, -.48f, 0), new Vector3(45f, .62f, length));
                for (int j = 0; j < 4; j++)
                    solid.Add(marker, new Vector3(sign * 6.37f, .51f, -length * .5f + 5f + j * 10f), new Vector3(.06f, .08f, .32f));

                // --- street lamps with halos and light pools ----------------
                for (int j = 0; j < 2; j++)
                {
                    float z = -length * .25f + j * length * .5f;
                    float sx = sign * 8.00f;
                    solid.Add(post, new Vector3(sx, 3.6f, z), new Vector3(.13f, 7.2f, .13f));
                    solid.Add(post, new Vector3(sx - sign * .88f, 7.07f, z), new Vector3(1.88f, .075f, .10f));
                    solid.Add(lamp, new Vector3(sx - sign * 1.74f, 6.98f, z), new Vector3(.54f, .09f, .30f));
                    // The emissive lamp and URP Bloom provide the glow. A separate additive
                    // billboard reads as a visible circular sprite when it approaches the camera.
                    glow.Flat(new Vector3(sign * 4.4f, -.03f, z), 6.8f, 14f, new Color(1f, .62f, .28f, .28f));
                }

                // --- near skyline: lit towers with neon signage -------------
                for (int b = 0; b < 3; b++)
                {
                    float z = -length * .36f + b * length * .34f;
                    float x = sign * (14f + rng.Next(0, 8));
                    float h = 9f + rng.Next(0, 12) * 1.65f;
                    float width = 3.6f + rng.Next(0, 4) * .75f;
                    float depth = 3.3f + rng.Next(0, 3) * .8f;
                    var facade = facades[rng.Next(0, facades.Length)];
                    solid.Add(facade, new Vector3(x, h * .5f - .42f, z), new Vector3(width, h, depth));
                    solid.Add(rail, new Vector3(x, h - .22f, z), new Vector3(width + .20f, .22f, depth + .20f));
                    float faceX = x - sign * (width * .5f + .011f);
                    int cols = Mathf.Max(2, Mathf.FloorToInt(depth / 1.15f));
                    int floors = Mathf.Min(14, Mathf.FloorToInt((h - 1.5f) / 2.3f));
                    for (int floor = 0; floor < floors; floor++)
                    {
                        float y = 1.5f + floor * 2.3f;
                        for (int column = 0; column < cols; column++)
                        {
                            if (rng.NextDouble() > .44) continue;
                            float wz = z - depth * .5f + (column + .5f) * depth / cols;
                            double pick = rng.NextDouble();
                            var wm = pick < .60 ? windAmber : (pick < .86 ? windCool : windRose);
                            solid.Add(wm, new Vector3(faceX, y, wz), new Vector3(.04f, .55f, depth / cols * .52f));
                        }
                    }
                    if (rng.NextDouble() < .68 && h > 12f)
                    {
                        int k = rng.Next(0, signMats.Length);
                        float signH = 2.5f + (float)rng.NextDouble() * 4f;
                        float signY = 3.5f + (float)rng.NextDouble() * (h - signH - 5f);
                        float signX = x - sign * (width * .5f + .07f);
                        solid.Add(signMats[k], new Vector3(signX, signY, z + (rng.NextDouble() < .5 ? -.7f : .7f)),
                            new Vector3(.10f, signH, .34f));
                        glow.Facade(new Vector3(signX - sign * .25f, signY, z), 3.4f, signH + 1.8f,
                            new Color(signColors[k].r, signColors[k].g, signColors[k].b, .55f));
                    }
                    if (h > 18f)
                    {
                        solid.Add(beacon, new Vector3(x, h + .05f, z), new Vector3(.34f, .34f, .34f));
                        glow.Billboard(new Vector3(x, h + .1f, z - 1f), 2.6f, 2.6f, new Color(1f, .15f, .12f, .75f));
                    }
                }

                // --- far skyline: tall silhouettes that dissolve into the fog
                for (int k = 0; k < 2; k++)
                {
                    float z = -length * .25f + k * length * .5f + ((float)rng.NextDouble() - .5f) * 6f;
                    float x = sign * (36f + (float)rng.NextDouble() * 12f);
                    float h = 24f + (float)rng.NextDouble() * 40f;
                    float width = 6f + (float)rng.NextDouble() * 4f;
                    float depth = 6f + (float)rng.NextDouble() * 4f;
                    solid.Add(facades[rng.Next(0, facades.Length)], new Vector3(x, h * .5f - .5f, z), new Vector3(width, h, depth));
                    float faceX = x - sign * (width * .5f + .02f);
                    for (int floor = 0; floor < 18; floor++)
                        for (int column = 0; column < 4; column++)
                        {
                            if (rng.NextDouble() > .20) continue;
                            float y = 2f + floor * 3f;
                            if (y > h - 2f) continue;
                            float wz = z - depth * .5f + (column + .5f) * depth / 4f;
                            solid.Add(rng.NextDouble() < .7 ? windAmber : windCool, new Vector3(faceX, y, wz), new Vector3(.05f, .8f, 1.0f));
                        }
                    if (rng.NextDouble() < .6)
                    {
                        solid.Add(beacon, new Vector3(x, h + .1f, z), new Vector3(.5f, .5f, .5f));
                        glow.Billboard(new Vector3(x, h + .1f, z - 1.5f), 4.5f, 4.5f, new Color(1f, .15f, .12f, .7f));
                    }
                }
            }

            // --- sparse overhead sign gantry ----------------------------------
            if (id % 3 == 0)
            {
                var panelMat = Mat("highway sign panel", new Color(.03f, .30f, .26f), true, false, .4f, 0f, 1.4f);
                var whiteMat = Mat("sign white paint", new Color(.9f, .95f, .95f), true, false, .4f, 0f, 1.6f);
                solid.Add(rail, new Vector3(0, 6.8f, 10f), new Vector3(12.8f, .14f, .18f));
                solid.Add(post, new Vector3(-6.4f, 3.4f, 10f), new Vector3(.24f, 6.9f, .24f));
                solid.Add(post, new Vector3(6.4f, 3.4f, 10f), new Vector3(.24f, 6.9f, .24f));
                solid.Add(post, new Vector3(0, 6.18f, 10f), new Vector3(3.6f, 1.10f, .14f));
                solid.Add(panelMat, new Vector3(0, 6.18f, 9.92f), new Vector3(3.4f, .96f, .04f));
                solid.Add(whiteMat, new Vector3(-.9f, 6.18f, 9.89f), new Vector3(.9f, .08f, .03f));
                solid.Add(whiteMat, new Vector3(.45f, 6.18f, 9.89f), new Vector3(1.4f, .08f, .03f));
                glow.Billboard(new Vector3(0, 6.18f, 9.6f), 5.2f, 2.2f, new Color(.2f, 1f, .8f, .28f));
            }

            solid.Build(root.transform);
            glow.Build(root.transform, "Halos and light pools");
            return root;
        }

        // ------------------------------------------------------------------ pickups

        public static GameObject CreateCoin(Transform parent)
        {
            var go = new GameObject("Pooled score coin");
            go.transform.SetParent(parent, false);
            var gold = Mat("coin gold", Gold, true, false, .9f, .7f, 1.2f);
            var inner = Mat("coin inner", new Color(1f, .58f, .16f), true, false, .8f, .6f, 1.4f);
            Cylinder(go.transform, "Coin rim", Vector3.zero, new Vector3(.66f, .07f, .66f), gold, Quaternion.Euler(0, 0, 90));
            Cylinder(go.transform, "Coin face", new Vector3(0, 0, 0), new Vector3(.46f, .085f, .46f), inner, Quaternion.Euler(0, 0, 90));
            Box(go.transform, "Coin emblem", Vector3.zero, new Vector3(.20f, .30f, .12f), gold);
            return go;
        }

        public static GameObject CreateFuel(Transform parent)
        {
            var go = new GameObject("Pooled fuel can");
            go.transform.SetParent(parent, false);
            var tank = Mat("fuel can body", Mint, true, false, .7f, .2f, 1.6f);
            var metal = Mat("fuel can metal", new Color(.18f, .22f, .27f), false, false, .8f, .8f);
            Box(go.transform, "Fuel tank", Vector3.zero, new Vector3(.85f, 1.1f, .44f), tank);
            Box(go.transform, "Cap", new Vector3(.24f, .64f, 0), new Vector3(.27f, .18f, .33f), metal);
            Box(go.transform, "Handle", new Vector3(-.16f, .64f, 0), new Vector3(.34f, .12f, .12f), metal);
            Box(go.transform, "Fuel mark", new Vector3(0, .07f, -.228f), new Vector3(.37f, .36f, .025f), Mat("fuel mark", new Color(.013f, .09f, .08f)));
            Box(go.transform, "Fuel mark bolt", new Vector3(0, .07f, -.242f), new Vector3(.12f, .26f, .012f), Mat("fuel mark bolt", new Color(.95f, 1f, .95f), true, false, .4f, 0f, 2f));
            return go;
        }

        // ------------------------------------------------------------------ sky

        // Preferred: procedural shader sky (no texture, always present in builds).
        // Fallback: a generated equirectangular panorama on the built-in Panoramic shader.
        public static bool ConfigureNightSkybox()
        {
            var sky = Resources.Load<Shader>("Shaders/NightSky");
            if (!sky) sky = Shader.Find("NeonHighway/NightSky");
            if (sky && sky.isSupported)
            {
                var mat = new Material(sky) { name = "Neon Highway | procedural night sky" };
                if (mat.HasProperty("_FogTint")) mat.SetColor("_FogTint", FogColor);
                RenderSettings.skybox = mat;
                DynamicGI.UpdateEnvironment();
                return true;
            }
            return ConfigureTexturedSkybox();
        }

        static bool ConfigureTexturedSkybox()
        {
            var shader = Shader.Find("Skybox/Panoramic");
            if (!shader || !shader.isSupported)
            {
                Debug.LogWarning("[Neon Highway] No skybox shader available; retaining procedural star objects.");
                return false;
            }
            const int width = 1024, height = 512;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Procedurally generated night panorama";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                float horizon = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v - .5f) * 3f), 2.2f);
                var sky = Color.Lerp(new Color(.006f, .009f, .034f), new Color(.40f, .12f, .30f), horizon);
                for (int x = 0; x < width; x++) pixels[y * width + x] = sky;
            }
            var rng = new System.Random(1864);
            for (int i = 0; i < 1550; i++)
            {
                int x = rng.Next(width), y = rng.Next(height / 2, height);
                float alpha = (float)(.3 + .7 * rng.NextDouble());
                var c = Color.Lerp(new Color(.63f, .73f, .79f), Color.white, (float)rng.NextDouble()) * alpha;
                pixels[y * width + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var skybox = new Material(shader) { name = "Neon Highway | fallback starry skybox" };
            skybox.SetTexture("_MainTex", texture);
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
            return true;
        }

        public static GameObject CreateMoonAndStars(Transform parent)
        {
            var sky = new GameObject("Moon and distant stars");
            sky.transform.SetParent(parent, false);
            var moonPos = new Vector3(-53, 70, 170);
            Sphere(sky.transform, "Moon", moonPos, new Vector3(13f, 13f, 13f),
                Mat("moon", new Color(.88f, .92f, 1f), true, false, .2f, 0f, 1.8f));
            // Two layered halos give the moon a soft atmospheric bloom even without post-processing.
            Halo(sky.transform, "Moon halo inner", moonPos + new Vector3(0, 0, -9f), new Vector2(46f, 46f), new Color(.60f, .72f, 1f, .60f), 1.5f);
            Halo(sky.transform, "Moon halo outer", moonPos + new Vector3(0, 0, -10f), new Vector2(120f, 120f), new Color(.45f, .40f, .90f, .32f), 1.2f);
            var rng = new System.Random(2026);
            var starMat = Mat("stars", new Color(.80f, .86f, 1f), true, false, .2f, 0f, 2.2f);
            for (int i = 0; i < 90; i++)
            {
                float x = (float)(rng.NextDouble() * 200 - 100);
                float y = (float)(rng.NextDouble() * 75 + 19);
                float z = (float)(rng.NextDouble() * 140 + 80);
                float r = (float)(rng.NextDouble() * .12 + .045);
                Sphere(sky.transform, "Star", new Vector3(x, y, z), new Vector3(r, r, r), starMat);
            }
            return sky;
        }
    }
}
