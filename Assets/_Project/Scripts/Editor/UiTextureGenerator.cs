using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// Genera le texture procedurali del HUD "armatura" e del menu in Assets/ThirdParty/_Own/Hud/ (binari fuori dal repo,
    /// come da ASSETS.md: ogni PC le rigenera identiche con Setup/7). Tutte bianche/grigie con alfa: il colore lo dà
    /// il foglio di stile (-unity-background-image-tint-color), così HUD ciano e menu ambra usano le stesse texture.
    /// </summary>
    public static class UiTextureGenerator
    {
        public const string Dir = "Assets/ThirdParty/_Own/Hud";
        public const string KenneyDir = "Assets/ThirdParty/Kenney/UIPackSciFi";
        public const string RubberduckDir = "Assets/ThirdParty/OpenGameArt/SciFiInterface";

        /// <summary>
        /// Ritagli dai pannelli di rubberduck (CC0, 2048x2048): file, rettangolo in pixel dall'alto a sinistra,
        /// maschera circolare (centro e raggio nel ritaglio, 0 = nessuna), lato lungo dell'output.
        /// </summary>
        private static readonly (string output, string file, RectInt crop, Vector2 center, float radius, int size)[] HoloCrops =
        {
            ("holo_ring", "interface_1_no_effects.png", new RectInt(70, 60, 770, 770), new Vector2(385f, 385f), 372f, 512),
            ("holo_radar", "interface_2_no_effects.png", new RectInt(270, 90, 530, 530), new Vector2(270f, 251f), 246f, 384),
            ("holo_wave", "interface_1_no_effects.png", new RectInt(1198, 812, 727, 334), Vector2.zero, 0f, 512),
        };

        /// <summary>Sagome di Kenney (CC0) da cui ricaviamo le piastre: nome file -> suffisso della piastra.</summary>
        private static readonly (string source, string plate)[] PlateShapes =
        {
            ("panel_glass_notch_bl", "notch_bl"),
            ("panel_glass_notch_br", "notch_br"),
            ("panel_glass_notch_tl", "notch_tl"),
            ("panel_glass_notch_tr", "notch_tr"),
            ("panel_glass_notches", "notches"),
            ("panel_glass_notches_top", "notches_top"),
            ("panel_glass", "plain"),
        };

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            Save("hex_tile", HexTile(56, 96), repeat: true);
            Save("scanlines", Scanlines(4, 4), repeat: true);
            Save("visor_frame", VisorFrame(1280, 720), repeat: false);
            Save("visor_vignette", Vignette(512, 288, 0.55f, 2.2f), repeat: false);
            Save("damage_vignette", Vignette(512, 288, 0.68f, 2.2f), repeat: false);
            Save("segment", Segment(24, 48, 7), repeat: false);
            Save("soft_backdrop", SoftBackdrop(256, 128), repeat: false);
            Save("glow_bar", GlowBar(64, 16), repeat: false);
            Save("compass_ticks", CompassTicks(1440, 40), repeat: true);
            Save("reticle_ring", ReticleRing(512), repeat: false);
            Save("menu_background", MenuBackground(1280, 720), repeat: false);
            Save("noise", Noise(128, 128, 1337), repeat: true);
            Save("hazard_stripes", Hazard(64, 16), repeat: true);
            int plates = DerivePlates();
            int holos = DeriveHolograms();
            AssetDatabase.Refresh();
            foreach (var crop in HoloCrops)
                if (File.Exists($"{Dir}/{crop.output}.png"))
                    ConfigureImporter($"{Dir}/{crop.output}.png", false);
            foreach ((string _, string plate) in PlateShapes)
            {
                ConfigureImporter($"{Dir}/plate_{plate}_fill.png", false);
                ConfigureImporter($"{Dir}/plate_{plate}_edge.png", false);
            }
            foreach (string name in new[] { "hex_tile", "scanlines", "visor_frame", "visor_vignette", "damage_vignette", "segment", "soft_backdrop",
                         "glow_bar", "compass_ticks", "reticle_ring", "menu_background", "noise", "hazard_stripes" })
                ConfigureImporter($"{Dir}/{name}.png", name is "hex_tile" or "scanlines" or "compass_ticks" or "noise" or "hazard_stripes");
            Debug.Log($"[FpsWave] Texture dell'interfaccia generate in {Dir} ({plates} piastre ricavate dalle sagome Kenney, {holos} ologrammi dai pannelli di rubberduck).");
        }

        /// <summary>
        /// Dalle sagome dei pannelli Kenney (angoli tagliati) ricava due maschere bianche per piastra: riempimento e bordo.
        /// Nel foglio di stile si colorano separatamente (vetro scuro + bordo luminoso) e si usano a 9 fette.
        /// Se il pacchetto Kenney manca (tools/setup-ui-assets.ps1 non eseguito) si usano rettangoli generati.
        /// </summary>
        private static int DerivePlates()
        {
            int derived = 0;
            foreach ((string source, string plate) in PlateShapes)
            {
                string path = Path.Combine(KenneyDir, source + ".png");
                float[] alpha;
                int w, h;
                if (File.Exists(path))
                {
                    var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    src.LoadImage(File.ReadAllBytes(path));
                    w = src.width;
                    h = src.height;
                    Color32[] px = src.GetPixels32();
                    alpha = new float[px.Length];
                    for (int i = 0; i < px.Length; i++)
                        alpha[i] = px[i].a / 255f;
                    UnityEngine.Object.DestroyImmediate(src);
                    derived++;
                }
                else
                {
                    w = h = 64;
                    alpha = new float[w * h];
                    for (int i = 0; i < alpha.Length; i++)
                        alpha[i] = 1f;
                }

                var fill = New(w, h);
                Fill(fill, (x, y) => alpha[y * w + x]);
                Save($"plate_{plate}_fill", fill, repeat: false);

                var edge = New(w, h);
                Fill(edge, (x, y) =>
                {
                    float a = alpha[y * w + x];
                    if (a < 0.5f)
                        return 0f;
                    // bordo = pixel pieno con un vuoto entro 2 px (o il limite dell'immagine)
                    for (int oy = -2; oy <= 2; oy++)
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || alpha[ny * w + nx] < 0.5f)
                                return Mathf.Abs(ox) + Mathf.Abs(oy) <= 2 ? 1f : 0.45f;
                        }
                    return 0f;
                });
                Save($"plate_{plate}_edge", edge, repeat: false);
            }
            return derived;
        }

        /// <summary>
        /// Ritaglia anelli, radar e onda dai pannelli di rubberduck e li rende "olografici": lo sfondo scuro diventa
        /// trasparente (alfa dalla luminosità), così stanno sopra la scena senza coprire la vista.
        /// </summary>
        private static int DeriveHolograms()
        {
            int done = 0;
            var cache = new System.Collections.Generic.Dictionary<string, Texture2D>();
            try
            {
                foreach (var crop in HoloCrops)
                {
                    string path = Path.Combine(RubberduckDir, crop.file);
                    if (!File.Exists(path))
                        continue;
                    if (!cache.TryGetValue(crop.file, out Texture2D src))
                    {
                        src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        src.LoadImage(File.ReadAllBytes(path));
                        cache[crop.file] = src;
                    }

                    float aspect = crop.crop.width / (float)crop.crop.height;
                    int w = aspect >= 1f ? crop.size : Mathf.RoundToInt(crop.size * aspect);
                    int h = aspect >= 1f ? Mathf.RoundToInt(crop.size / aspect) : crop.size;
                    var outTex = New(w, h);
                    var pixels = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            // coordinate nel ritaglio (dall'alto a sinistra, come nell'immagine)
                            float cx = (x + 0.5f) / w * crop.crop.width;
                            float cyTop = (1f - (y + 0.5f) / h) * crop.crop.height;
                            float u = (crop.crop.x + cx) / src.width;
                            float v = 1f - (crop.crop.y + cyTop) / src.height;
                            Color c = src.GetPixelBilinear(u, v);
                            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                            float a = Smooth(0.14f, 0.42f, lum) * c.a;
                            if (crop.radius > 0f)
                            {
                                float d = Vector2.Distance(new Vector2(cx, cyTop), crop.center);
                                a *= 1f - Smooth(crop.radius - 3f, crop.radius + 1f, d);
                            }
                            pixels[y * w + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(Mathf.Clamp01(a) * 255f));
                        }
                    outTex.SetPixels32(pixels);
                    outTex.Apply(false);
                    Save(crop.output, outTex, repeat: false);
                    done++;
                }
            }
            finally
            {
                foreach (Texture2D t in cache.Values)
                    UnityEngine.Object.DestroyImmediate(t);
            }
            return done;
        }

        // ------------------------------------------------------------------ Salvataggio e import

        private static void Save(string name, Texture2D texture, bool repeat)
        {
            texture.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void ConfigureImporter(string path, bool repeat)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static Texture2D New(int w, int h) => new(w, h, TextureFormat.RGBA32, false, false);

        private static void Fill(Texture2D t, Func<int, int, float> alpha, float value = 1f)
        {
            var pixels = new Color32[t.width * t.height];
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                {
                    float a = Mathf.Clamp01(alpha(x, y));
                    byte v = (byte)(value * 255f);
                    pixels[y * t.width + x] = new Color32(v, v, v, (byte)(a * 255f));
                }
            t.SetPixels32(pixels);
            t.Apply(false);
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ Generatori

        /// <summary>Griglia esagonale (esagoni a punta in alto), ripetibile senza giunte.</summary>
        private static Texture2D HexTile(int w, int h)
        {
            var t = New(w, h);
            float r = h / 3f;                 // raggio circoscritto
            float sx = w / (Mathf.Sqrt(3f) * r); // piccola correzione per far coincidere il periodo con i pixel
            Vector2[] centers =
            {
                new(0f, 0f), new(w, 0f), new(0f, h), new(w, h),
                new(w * 0.5f, 1.5f * r), new(w * 0.5f, -1.5f * r), new(w * 0.5f, 4.5f * r),
                new(-w * 0.5f, 1.5f * r), new(w * 1.5f, 1.5f * r),
            };
            Fill(t, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float best = float.MaxValue;
                foreach (Vector2 c in centers)
                {
                    float dx = Mathf.Abs(p.x - c.x) / sx, dy = Mathf.Abs(p.y - c.y);
                    float d = Mathf.Max(dx, 0.5f * dx + 0.8660254f * dy) / (0.8660254f * r);
                    best = Mathf.Min(best, d);
                }
                return Smooth(0.9f, 0.97f, best);
            });
            return t;
        }

        private static Texture2D Scanlines(int w, int h)
        {
            var t = New(w, h);
            Fill(t, (x, y) => y == 0 ? 1f : 0f);
            return t;
        }

        /// <summary>Cornice del visore: archi agli angoli, tacche ai bordi, linee sottili. Bianca, da colorare.</summary>
        private static Texture2D VisorFrame(int w, int h)
        {
            var t = New(w, h);
            float cornerR = h * 0.16f;
            Fill(t, (x, y) =>
            {
                float a = 0f;
                // archi agli angoli (quarti di cerchio a distanza dal bordo)
                foreach (var c in new[] { new Vector2(cornerR + 30, cornerR + 30), new Vector2(w - cornerR - 30, cornerR + 30),
                             new Vector2(cornerR + 30, h - cornerR - 30), new Vector2(w - cornerR - 30, h - cornerR - 30) })
                {
                    float dx = x - c.x, dy = y - c.y;
                    bool outward = (dx * (c.x < w * 0.5f ? -1 : 1) >= 0f) && (dy * (c.y < h * 0.5f ? -1 : 1) >= 0f);
                    if (!outward)
                        continue;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - cornerR);
                    a = Mathf.Max(a, 1f - Smooth(0.6f, 1.8f, d));
                    float d2 = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - cornerR - 8f);
                    a = Mathf.Max(a, 0.35f * (1f - Smooth(0.4f, 1.2f, d2)));
                }
                // tacche lungo il bordo superiore e inferiore
                int margin = 14;
                bool nearTop = y >= h - margin - 10 && y <= h - margin;
                bool nearBottom = y >= margin && y <= margin + 10;
                if ((nearTop || nearBottom) && x > w * 0.3f && x < w * 0.7f && (x % 24) < 2)
                    a = Mathf.Max(a, (x % 120) < 2 ? 0.9f : 0.45f);
                // linee orizzontali sottili ai lati del centro in alto
                if (Mathf.Abs(y - (h - margin - 14)) < 1f && x > w * 0.32f && x < w * 0.68f)
                    a = Mathf.Max(a, 0.25f);
                return a;
            });
            return t;
        }

        /// <summary>Vignetta: alfa che cresce verso i bordi (start = dove inizia, power = curva).</summary>
        private static Texture2D Vignette(int w, int h, float start, float power)
        {
            var t = New(w, h);
            Fill(t, (x, y) =>
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(u * u * 0.85f + v * v * 1.15f);
                return Mathf.Pow(Smooth(start, 1.35f, d), power);
            });
            return t;
        }

        /// <summary>Fondo sfumato ellittico (scuro al centro, trasparente ai bordi) dietro ai blocchi del HUD minimale.</summary>
        private static Texture2D SoftBackdrop(int w, int h)
        {
            var t = New(w, h);
            Fill(t, (x, y) =>
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                return 1f - Smooth(0.15f, 1f, d);
            });
            return t;
        }

        /// <summary>Segmento inclinato (parallelogramma) per barre di salute e munizioni.</summary>
        private static Texture2D Segment(int w, int h, int slant)
        {
            var t = New(w, h);
            Fill(t, (x, y) =>
            {
                float shift = slant * (y / (float)(h - 1));
                float left = shift, right = w - slant + shift;
                float a = Smooth(left - 0.5f, left + 0.8f, x + 0.5f) * (1f - Smooth(right - 0.8f, right + 0.5f, x + 0.5f));
                float top = 1f - 0.25f * (y / (float)h); // leggermente più luminoso in alto
                return a * top;
            });
            return t;
        }

        private static Texture2D GlowBar(int w, int h)
        {
            var t = New(w, h);
            Fill(t, (x, y) =>
            {
                float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                return Mathf.Pow(1f - v, 2.2f);
            });
            return t;
        }

        /// <summary>Bussola: 4 px per grado, tacca ogni 5°, media ogni 15°, lunga ogni 45°. Ripetibile in orizzontale.</summary>
        private static Texture2D CompassTicks(int w, int h)
        {
            var t = New(w, h);
            float ppd = w / 360f;
            Fill(t, (x, y) =>
            {
                float deg = x / ppd;
                float m5 = deg % 5f, m15 = deg % 15f, m45 = deg % 45f;
                float len = m45 < 1f / ppd ? 1f : m15 < 1f / ppd ? 0.6f : m5 < 1f / ppd ? 0.35f : 0f;
                if (len <= 0f)
                    return Mathf.Abs(y - 1) < 1 ? 0.25f : 0f; // linea di base
                return y <= h * len ? (len >= 1f ? 1f : 0.7f) : 0f;
            });
            return t;
        }

        /// <summary>Anello del mirino: cerchio sottile interrotto, tacche ogni 15° e quattro staffe.</summary>
        private static Texture2D ReticleRing(int size)
        {
            var t = New(size, size);
            float c = size * 0.5f, r = size * 0.42f;
            Fill(t, (x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
                float a = 0f;
                bool gap = (ang % 90f) < 14f || (ang % 90f) > 76f; // varchi in corrispondenza degli assi
                if (!gap)
                    a = Mathf.Max(a, 1f - Smooth(0.8f, 2.2f, Mathf.Abs(dist - r)));
                if ((ang % 15f) < 0.7f && dist > r + 6f && dist < r + 16f)
                    a = Mathf.Max(a, 0.8f);
                // staffe agli assi
                if ((ang % 90f) < 6f || (ang % 90f) > 84f)
                    a = Mathf.Max(a, dist > r - 22f && dist < r - 8f ? 0.9f : 0f);
                float inner = Mathf.Abs(dist - r * 0.62f);
                if ((ang % 30f) < 10f)
                    a = Mathf.Max(a, 0.35f * (1f - Smooth(0.5f, 1.5f, inner)));
                return a;
            });
            return t;
        }

        private static Texture2D MenuBackground(int w, int h)
        {
            var t = New(w, h);
            var rng = new System.Random(7);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    // luce diffusa dall'alto a sinistra + vignetta
                    float glow = Mathf.Exp(-((u - 0.25f) * (u - 0.25f) * 3f + (v - 0.7f) * (v - 0.7f) * 4f));
                    float vig = 1f - Smooth(0.45f, 1.1f, Mathf.Sqrt((u - 0.5f) * (u - 0.5f) * 2.2f + (v - 0.5f) * (v - 0.5f) * 3f));
                    float grain = (float)rng.NextDouble() * 0.04f;
                    float l = Mathf.Clamp01(0.05f + 0.16f * glow * vig + grain);
                    byte b = (byte)(l * 255f);
                    pixels[y * w + x] = new Color32(b, b, b, 255);
                }
            t.SetPixels32(pixels);
            t.Apply(false);
            return t;
        }

        private static Texture2D Noise(int w, int h, int seed)
        {
            var t = New(w, h);
            var rng = new System.Random(seed);
            var values = new float[w * h];
            for (int i = 0; i < values.Length; i++)
                values[i] = (float)rng.NextDouble();
            Fill(t, (x, y) => values[y * w + x]);
            return t;
        }

        private static Texture2D Hazard(int w, int h)
        {
            var t = New(w, h);
            Fill(t, (x, y) => ((x + y) % 16) < 8 ? 1f : 0f);
            return t;
        }
    }
}
