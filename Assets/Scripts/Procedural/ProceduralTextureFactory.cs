using UnityEngine;

namespace Football.Procedural
{
    /// <summary>
    /// Generates high-resolution procedural textures at runtime for grass turf,
    /// World Cup match balls, striped team jerseys, goal nets, and glowing LED advertising ribbons.
    /// </summary>
    public static class ProceduralTextureFactory
    {
        public static Texture2D CreateTurfGrassTexture(Color baseColor, Color altColor)
        {
            int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;

                    // 1. Manicured lawn roller stripe sheen & multi-frequency Perlin noise
                    float n1 = Mathf.PerlinNoise(nx * 12f, ny * 12f);
                    float n2 = Mathf.PerlinNoise(nx * 36f, ny * 36f) * 0.45f;
                    float n3 = Mathf.PerlinNoise(nx * 84f, ny * 84f) * 0.25f;
                    float blend = Mathf.Clamp01((n1 + n2 + n3) * 0.72f);

                    Color c = Color.Lerp(baseColor, altColor, blend);

                    // 2. High-density grass blade fibers (vertical micro-stripes simulating manicured blades)
                    float bladePattern = Mathf.Sin(nx * 480f) * Mathf.Cos(ny * 180f);
                    if (bladePattern > 0.4f)
                    {
                        c = Color.Lerp(c, baseColor * 1.14f, 0.35f);
                    }
                    else if (bladePattern < -0.4f)
                    {
                        c = Color.Lerp(c, altColor * 0.88f, 0.30f);
                    }

                    // 3. Subtle organic blade brightness jitter
                    float bladeJitter = (((x * 37 + y * 73) ^ (x * y * 19)) % 11) * 0.009f;
                    c.r = Mathf.Clamp01(c.r + bladeJitter);
                    c.g = Mathf.Clamp01(c.g + bladeJitter * 1.3f); // Enhance healthy chlorophyll green
                    c.b = Mathf.Clamp01(c.b + bladeJitter * 0.7f);

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateAlRihlaBallTexture()
        {
            int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;

            Color white = new Color(0.96f, 0.96f, 0.98f);
            Color cyan = new Color(0.05f, 0.82f, 0.95f);
            Color gold = new Color(0.95f, 0.78f, 0.15f);
            Color magenta = new Color(0.92f, 0.15f, 0.45f);
            Color black = new Color(0.1f, 0.1f, 0.12f);

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float ny = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    Color c = white;

                    // Triangular dynamic speed ribbons
                    float distCenter = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    float angle = Mathf.Atan2(ny - 0.5f, nx - 0.5f);

                    float swirl = Mathf.Sin(angle * 5f + distCenter * 14f);
                    if (swirl > 0.65f && distCenter < 0.45f && distCenter > 0.08f)
                    {
                        c = Color.Lerp(cyan, magenta, (Mathf.Sin(angle * 2f) + 1f) * 0.5f);
                    }
                    else if (swirl > 0.45f && distCenter < 0.42f && distCenter > 0.12f)
                    {
                        c = gold;
                    }

                    // Seam lines between panels
                    float seam = Mathf.Sin(nx * 32f) * Mathf.Cos(ny * 32f);
                    if (Mathf.Abs(seam) > 0.94f)
                    {
                        c = Color.Lerp(c, black, 0.45f);
                    }

                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateStripedJerseyTexture(Color c1, Color c2, int stripeCount = 6)
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;

            Color[] pixels = new Color[w * h];
            float stripeWidth = (float)w / stripeCount;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int stripeIdx = Mathf.FloorToInt(x / stripeWidth);
                    Color baseCol = (stripeIdx % 2 == 0) ? c1 : c2;

                    // Gold chest badge patch on top left
                    if (x > w * 0.2f && x < w * 0.35f && y > h * 0.65f && y < h * 0.80f)
                    {
                        baseCol = new Color(0.95f, 0.82f, 0.2f);
                    }

                    pixels[y * w + x] = baseCol;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateArgentinaKitTexture()
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color skyBlue = new Color(0.44f, 0.72f, 0.98f);
            Color white = new Color(0.97f, 0.97f, 0.97f);
            Color gold = new Color(0.96f, 0.82f, 0.18f);
            Color black = new Color(0.12f, 0.12f, 0.14f);

            Color[] pixels = new Color[w * h];
            float stripeWidth = (float)w / 6f; // Classic Albiceleste vertical stripes

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    int stripeIdx = Mathf.FloorToInt(x / stripeWidth);
                    Color c = (stripeIdx % 2 == 0) ? skyBlue : white;

                    // Black collar & sleeve trim
                    if (ny > 0.92f || ny < 0.05f)
                    {
                        c = black;
                    }
                    // Gold AFA 3-Stars Crest on left chest
                    else if (nx >= 0.22f && nx <= 0.36f && ny >= 0.64f && ny <= 0.82f)
                    {
                        c = gold;
                    }
                    // Gold FIFA World Champions shield in center chest
                    else if (nx >= 0.44f && nx <= 0.56f && ny >= 0.68f && ny <= 0.80f)
                    {
                        c = gold;
                    }
                    // Subtle fabric texture noise
                    float grain = ((x * 13 + y * 29) % 5) * 0.015f;
                    c.r += grain; c.g += grain; c.b += grain;

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateFranceKitTexture()
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color crimsonRed = new Color(0.88f, 0.12f, 0.16f); // Vivid World Cup Crimson / Scarlet
            Color darkRed = new Color(0.72f, 0.08f, 0.12f);
            Color gold = new Color(0.96f, 0.82f, 0.18f); // Golden FFF Rooster Emblem
            Color navy = new Color(0.06f, 0.10f, 0.28f); // French Royal Midnight Navy accents
            Color white = new Color(0.96f, 0.96f, 0.96f);

            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    Color c = crimsonRed;

                    // Modern athletic dynamic wave pattern
                    float wave = Mathf.Sin(nx * 28f + ny * 8f);
                    if (wave > 0.35f) c = darkRed;

                    // French Tricolor Flag ribbing at collar edge (Blue, White, Red)
                    if (ny > 0.94f)
                    {
                        if (nx < 0.33f) c = new Color(0.08f, 0.25f, 0.75f);
                        else if (nx < 0.66f) c = white;
                        else c = crimsonRed;
                    }
                    else if (ny > 0.88f && ny <= 0.94f)
                    {
                        c = navy; // Navy crewneck collar
                    }
                    // Gold FFF Rooster Emblem on left chest
                    else if (nx >= 0.22f && nx <= 0.36f && ny >= 0.65f && ny <= 0.82f)
                    {
                        c = gold;
                    }
                    // Gold two-star champion badge on upper chest
                    else if (nx >= 0.26f && nx <= 0.32f && ny >= 0.83f && ny <= 0.87f)
                    {
                        c = gold;
                    }

                    // Bottom hem trim (navy blue band)
                    if (ny < 0.06f)
                    {
                        c = navy;
                    }

                    // Subtle fabric micro-grain
                    float grain = ((x * 17 + y * 31) % 7) * 0.012f;
                    c.r += grain; c.g += grain; c.b += grain;

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateGoalkeeperKitTexture(Color baseNeon)
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;

            Color darkTrim = new Color(0.08f, 0.08f, 0.10f);
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Dynamic diamond hex pattern for modern GK kits
                    bool hex = (x % 32 < 4) || (y % 32 < 4) || ((x + y) % 32 < 4);
                    Color c = hex ? Color.Lerp(baseNeon, darkTrim, 0.25f) : baseNeon;

                    // Collar trim
                    if (y > h * 0.93f) c = darkTrim;

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateCrowdTexture()
        {
            int w = 1024, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;

            Color skyBlue = new Color(0.44f, 0.72f, 0.98f);
            Color white = new Color(0.96f, 0.96f, 0.96f);
            Color franceNavy = new Color(0.06f, 0.12f, 0.32f);
            Color crimsonRed = new Color(0.86f, 0.14f, 0.18f);
            Color fanGold = new Color(0.96f, 0.82f, 0.18f);
            Color darkJacket = new Color(0.12f, 0.13f, 0.16f);
            Color concreteBase = new Color(0.28f, 0.30f, 0.34f);
            Color seatPlastic = new Color(0.16f, 0.22f, 0.38f);
            Color shadowCol = new Color(0.08f, 0.09f, 0.11f);

            Color[] skinPalette = {
                new Color(0.92f, 0.76f, 0.62f),
                new Color(0.85f, 0.68f, 0.54f),
                new Color(0.68f, 0.48f, 0.35f),
                new Color(0.48f, 0.32f, 0.22f),
                new Color(0.95f, 0.82f, 0.72f)
            };

            Color[] hairPalette = {
                new Color(0.10f, 0.08f, 0.07f), // Black
                new Color(0.28f, 0.18f, 0.10f), // Brown
                new Color(0.55f, 0.38f, 0.18f), // Light Brown
                new Color(0.82f, 0.70f, 0.35f)  // Blonde
            };

            Color[] pixels = new Color[w * h];

            int rowHeight = 32;
            int seatWidth = 16;
            int numRows = h / rowHeight;     // 16 rows
            int numSeats = w / seatWidth;    // 64 seats per row

            for (int r = 0; r < numRows; r++)
            {
                int rowY = r * rowHeight;
                for (int s = 0; s < numSeats; s++)
                {
                    int seatX = s * seatWidth;
                    bool isAisle = (s % 16 == 0);

                    // Seeded random traits for this spectator
                    int hash = (s * 97 + r * 223) ^ (s * r * 31);
                    int skinIdx = Mathf.Abs(hash) % skinPalette.Length;
                    int hairIdx = Mathf.Abs(hash >> 3) % hairPalette.Length;
                    int teamChoice = Mathf.Abs(hash >> 5) % 10; // 0-3 Arg, 4-7 Fra, 8-9 Neutral
                    int poseType = Mathf.Abs(hash >> 7) % 6;    // 0,1: regular, 2: arms raised, 3: scarf overhead, 4,5: chanting
                    bool hasHat = ((hash >> 9) & 1) == 1;
                    Color skinColor = skinPalette[skinIdx];
                    Color hairColor = hairPalette[hairIdx];

                    // Draw the 16x32 cell
                    for (int ly = 0; ly < rowHeight; ly++)
                    {
                        int y = rowY + ly;
                        for (int lx = 0; lx < seatWidth; lx++)
                        {
                            int x = seatX + lx;

                            // 1. Concrete Aisle Stairway
                            if (isAisle)
                            {
                                Color c = concreteBase;
                                // Step edge highlight
                                if (ly == 0 || ly == 1) c = new Color(0.38f, 0.40f, 0.44f);
                                // Yellow safety edge
                                if (lx == 0 || lx == seatWidth - 1) c = new Color(0.85f, 0.75f, 0.15f);
                                pixels[y * w + x] = c;
                                continue;
                            }

                            // 2. Concrete riser and shadow below seat
                            if (ly < 4)
                            {
                                pixels[y * w + x] = (ly < 2) ? shadowCol : concreteBase;
                                continue;
                            }

                            // 3. Stadium Seat Shell Backdrop
                            Color pixelColor = concreteBase;
                            if (lx >= 2 && lx <= 13 && ly >= 4 && ly <= 26)
                            {
                                pixelColor = seatPlastic;
                                if (lx == 2 || lx == 13 || ly == 4) pixelColor = shadowCol;
                            }

                            // 4. Spectator Body / Torso (ly: 6 to 20, lx: 4 to 11)
                            if (lx >= 4 && lx <= 11 && ly >= 6 && ly <= 20)
                            {
                                if (teamChoice < 4)
                                {
                                    // Argentina Albiceleste Kit (vertical stripes)
                                    pixelColor = (lx % 3 == 0) ? white : skyBlue;
                                }
                                else if (teamChoice < 7)
                                {
                                    // France Navy Jersey with crimson collar
                                    pixelColor = (ly >= 18) ? crimsonRed : franceNavy;
                                }
                                else if (teamChoice == 7)
                                {
                                    // Fan gold t-shirt
                                    pixelColor = fanGold;
                                }
                                else
                                {
                                    // Dark stadium jacket / hoodie
                                    pixelColor = (teamChoice == 8) ? darkJacket : white;
                                }
                            }

                            // 5. Spectator Head & Face (ly: 21 to 27, lx: 6 to 9)
                            if (lx >= 6 && lx <= 9 && ly >= 21 && ly <= 27)
                            {
                                pixelColor = skinColor;

                                // Hair or Cap on top
                                if (ly >= 25)
                                {
                                    if (hasHat)
                                    {
                                        pixelColor = (teamChoice < 4) ? skyBlue : (teamChoice < 7 ? franceNavy : crimsonRed);
                                    }
                                    else
                                    {
                                        pixelColor = hairColor;
                                    }
                                }
                                else if (ly == 24 && (lx == 7 || lx == 8))
                                {
                                    // Eye details
                                    pixelColor = new Color(0.2f, 0.15f, 0.1f);
                                }
                            }

                            // 6. Dynamic Cheering Arms & Team Scarf Overhead
                            if (poseType == 2)
                            {
                                // Arms raised cheering high
                                if ((lx == 3 || lx == 12) && ly >= 18 && ly <= 28)
                                {
                                    pixelColor = (ly >= 26) ? skinColor : ((teamChoice < 4) ? skyBlue : franceNavy);
                                }
                            }
                            else if (poseType == 3)
                            {
                                // Team Scarf held horizontally overhead!
                                if (ly >= 27 && ly <= 30 && lx >= 2 && lx <= 13)
                                {
                                    if (teamChoice < 4)
                                    {
                                        pixelColor = (lx % 4 < 2) ? skyBlue : white;
                                    }
                                    else
                                    {
                                        int flagPart = (lx - 2) / 4;
                                        pixelColor = (flagPart == 0) ? franceNavy : ((flagPart == 1) ? white : crimsonRed);
                                    }
                                }
                                else if ((lx == 3 || lx == 12) && ly >= 18 && ly <= 26)
                                {
                                    pixelColor = skinColor; // Arms holding scarf
                                }
                            }

                            pixels[y * w + x] = pixelColor;
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateLEDAdTexture()
        {
            int w = 1024, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color eaEmerald = new Color(0.04f, 0.14f, 0.11f);
            Color eaVolt = new Color(0.22f, 0.98f, 0.28f);
            Color fifaNavy = new Color(0.02f, 0.05f, 0.18f);
            Color fifaGold = new Color(0.98f, 0.82f, 0.20f);
            Color adidasBlack = new Color(0.08f, 0.08f, 0.09f);
            Color qatarBurgundy = new Color(0.38f, 0.04f, 0.18f);
            Color hyundaiBlue = new Color(0.00f, 0.16f, 0.44f);
            Color visaNavy = new Color(0.08f, 0.12f, 0.42f);
            Color white = Color.white;

            Color[] pixels = new Color[w * h];
            int blockWidth = w / 6; // ~170 pixels per sponsor brand block

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    int block = (x / blockWidth) % 6;
                    int localX = x % blockWidth;
                    float nlx = (float)localX / blockWidth;

                    Color c;

                    switch (block)
                    {
                        case 0: // EA SPORTS FC
                            c = eaEmerald;
                            // Neon triangle FC logo
                            if (localX >= 24 && localX <= 60 && y >= 32 && y <= 96)
                            {
                                float triDist = Mathf.Abs(localX - 42) / 18f;
                                if ((y - 32) / 64f >= triDist) c = eaVolt;
                            }
                            // EA SPORTS text banner
                            else if (localX > 68 && localX < 155 && y >= 46 && y <= 82)
                            {
                                c = white;
                            }
                            break;

                        case 1: // FIFA WORLD CUP 2026
                            c = fifaNavy;
                            // FIFA Trophy gold silhouette
                            if (localX >= 28 && localX <= 56 && y >= 28 && y <= 100)
                            {
                                c = fifaGold;
                            }
                            // Official World Cup text bar
                            else if (localX > 64 && localX < 155 && y >= 44 && y <= 84)
                            {
                                c = ((localX + y) % 8 < 2) ? fifaGold : white;
                            }
                            break;

                        case 2: // ADIDAS
                            c = adidasBlack;
                            // 3 iconic diagonal stripes
                            if (y >= 34 && y <= 94)
                            {
                                int diagX = localX - (y - 34) / 2;
                                if ((diagX >= 28 && diagX <= 38) || (diagX >= 46 && diagX <= 56) || (diagX >= 64 && diagX <= 74))
                                {
                                    c = white;
                                }
                            }
                            // ADIDAS logo typography block
                            if (localX >= 88 && localX <= 152 && y >= 48 && y <= 78)
                            {
                                c = white;
                            }
                            break;

                        case 3: // QATAR AIRWAYS
                            c = qatarBurgundy;
                            // Oryx antelope logo emblem
                            if (localX >= 22 && localX <= 58 && y >= 36 && y <= 92)
                            {
                                c = white;
                            }
                            // QATAR lettering bar
                            else if (localX >= 66 && localX <= 154 && y >= 46 && y <= 82)
                            {
                                c = fifaGold;
                            }
                            break;

                        case 4: // HYUNDAI
                            c = hyundaiBlue;
                            // Slanted metallic oval badge
                            if (localX >= 22 && localX <= 62 && y >= 34 && y <= 94)
                            {
                                float dx = (localX - 42) / 18f;
                                float dy = (y - 64) / 28f;
                                float dist = dx * dx + dy * dy;
                                if (dist >= 0.65f && dist <= 1.0f) c = white;
                            }
                            // HYUNDAI text ribbon
                            else if (localX >= 72 && localX <= 154 && y >= 46 && y <= 80)
                            {
                                c = white;
                            }
                            break;

                        default: // VISA
                            c = visaNavy;
                            // VISA typography with gold wing
                            if (localX >= 30 && localX <= 145 && y >= 44 && y <= 84)
                            {
                                c = (localX < 55 && y > 68) ? fifaGold : white;
                            }
                            break;
                    }

                    // Intense glowing electronic neon LED borders (top and bottom ticker edge)
                    if (y < 8 || y > 119)
                    {
                        c = fifaGold * 1.5f;
                    }
                    else if (y == 8 || y == 9 || y == 118 || y == 117)
                    {
                        c = Color.cyan * 1.3f;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateBannerTexture()
        {
            int w = 512, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color skyBlue = new Color(0.44f, 0.72f, 0.98f);
            Color white = Color.white;
            Color gold = new Color(0.96f, 0.82f, 0.18f);
            Color darkNavy = new Color(0.06f, 0.10f, 0.28f);
            Color crimson = new Color(0.88f, 0.12f, 0.16f);

            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    Color c;

                    if (nx < 0.45f)
                    {
                        // Argentina Banner: "VAMOS ARGENTINA" with sun badge
                        int stripe = (y / 21) % 3;
                        c = (stripe == 1) ? white : skyBlue;
                        if (nx > 0.08f && nx < 0.38f && y > 38 && y < 90)
                        {
                            c = gold;
                        }
                    }
                    else if (nx > 0.55f)
                    {
                        // France Banner: "ALLEZ LES BLEUS" tricolor
                        float localFraX = (nx - 0.55f) / 0.45f;
                        if (localFraX < 0.33f) c = darkNavy;
                        else if (localFraX < 0.66f) c = white;
                        else c = crimson;

                        if (localFraX > 0.15f && localFraX < 0.85f && y > 38 && y < 90)
                        {
                            c = gold;
                        }
                    }
                    else
                    {
                        // Center FIFA World Cup shield
                        c = darkNavy;
                        if (y > 28 && y < 100) c = gold;
                    }

                    // Gold decorative fringe border
                    if (y < 6 || y > 121) c = gold;

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateGoalNetTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Color netCord = new Color(0.95f, 0.95f, 0.95f, 0.85f);
            Color empty = new Color(0f, 0f, 0f, 0.05f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Diamond hexagonal grid mesh
                    bool cord = (x % 16 <= 2) || (y % 16 <= 2) || ((x + y) % 24 <= 2);
                    pixels[y * size + x] = cord ? netCord : empty;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateArgentinaFlagTexture()
        {
            int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color skyBlue = new Color(0.44f, 0.72f, 0.98f);
            Color white = new Color(0.98f, 0.98f, 0.98f);
            Color sunGold = new Color(0.96f, 0.78f, 0.12f);
            Color sunBrown = new Color(0.70f, 0.45f, 0.10f);

            Color[] pixels = new Color[w * h];
            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            float sunRadius = h * 0.13f;

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / h;
                Color stripeCol = (ny < 0.333f || ny > 0.667f) ? skyBlue : white;

                for (int x = 0; x < w; x++)
                {
                    Color c = stripeCol;
                    if (ny >= 0.333f && ny <= 0.667f)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist <= sunRadius)
                        {
                            c = (dist >= sunRadius - 1.2f) ? sunBrown : sunGold;
                        }
                        else if (dist <= sunRadius * 1.85f)
                        {
                            float angle = Mathf.Atan2(y - center.y, x - center.x);
                            float ray = Mathf.Sin(angle * 16f);
                            if (ray > 0.55f && dist <= sunRadius * (1.1f + ray * 0.75f))
                            {
                                c = sunGold;
                            }
                        }
                    }
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateFranceFlagTexture()
        {
            int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color blue = new Color(0.00f, 0.14f, 0.58f);
            Color white = new Color(0.98f, 0.98f, 0.98f);
            Color red = new Color(0.93f, 0.11f, 0.14f);

            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    Color c = (nx < 0.333f) ? blue : ((nx < 0.667f) ? white : red);
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
