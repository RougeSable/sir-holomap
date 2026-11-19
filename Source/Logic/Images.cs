using System;

namespace SirHolomap
{
    // Les images que la carte peint elle-même, en octets RGBA (alpha droit, sRGB) :
    // formes des repères, galaxie, globes de la vue Système. Rien n'est
    // livré avec le greffon ; tout est calculé sur la machine du joueur.
    public static class Images
    {
        public const int ShapeSize = 64;

        // Les pentes du relief sont accentuées de cette valeur sur les petits globes,
        // pour que les chaînes de montagnes se lisent d'un coup d'œil.
        public const double ReliefExaggeration = 1.6;

        public enum Shape
        {
            Disc,
            SoftDisc,
            Ring,
            Glow,
            Triangle,
            Diamond,
            Square,
            Hexagon,
            Brackets,
            Arrow,
            // Une étoile du ciel : un cœur rond et doux dans un halo léger.
            Star,
        }

        // Des formes blanches, teintées au dessin.
        public static byte[] ShapeImage(Shape shape)
        {
            var size = ShapeSize;
            var data = new byte[size * size * 4];
            var half = size / 2.0;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    // Centre du pixel, en unités où la forme s'étend sur [-1, 1].
                    var x = (px + 0.5 - half) / half;
                    var y = (py + 0.5 - half) / half;
                    var a = Coverage(shape, x, y, 1.0 / half);
                    var i = (py * size + px) * 4;
                    data[i] = 255;
                    data[i + 1] = 255;
                    data[i + 2] = 255;
                    data[i + 3] = ToByte(a);
                }
            }
            return data;
        }

        private static double Coverage(Shape shape, double x, double y, double pixel)
        {
            var r = Math.Sqrt(x * x + y * y);
            switch (shape)
            {
                case Shape.Disc:
                    return Edge(r - 0.92, pixel);
                case Shape.SoftDisc:
                    return Smooth(1 - Clamp01((r - 0.55) / 0.42));
                case Shape.Ring:
                    return Edge(Math.Abs(r - 0.8) - 0.12, pixel);
                case Shape.Glow:
                    return Math.Pow(Clamp01(1 - r), 2.2);
                case Shape.Triangle:
                    return Edge(TriangleDistance(x, y, 0.9), pixel);
                case Shape.Diamond:
                    return Edge((Math.Abs(x) + Math.Abs(y)) * 0.7071 - 0.66, pixel);
                case Shape.Square:
                    return Edge(Math.Max(Math.Abs(x), Math.Abs(y)) - 0.78, pixel);
                case Shape.Hexagon:
                    return Edge(Math.Abs(HexagonDistance(x, y, 0.8)) - 0.07, pixel);
                case Shape.Brackets:
                    return Edge(BracketDistance(x, y), pixel);
                case Shape.Arrow:
                    return Edge(TriangleDistance(y, -x, 0.9), pixel);
                case Shape.Star:
                    var core = Smooth(Clamp01((0.24 - r) / 0.1));
                    var halo = 0.5 * Math.Pow(Clamp01(1 - r), 2.6);
                    return core + halo * (1 - core);
            }
            return 0;
        }

        private static double Edge(double distance, double pixel)
        {
            return Clamp01(0.5 - distance / (pixel * 1.5));
        }

        // Un triangle pointant vers le haut (pointant vers -y à l'écran).
        private static double TriangleDistance(double x, double y, double size)
        {
            const double k = 1.7320508;
            x = Math.Abs(x) - size;
            y = -y + size / k;
            if (x + k * y > 0)
            {
                var nx = (x - k * y) / 2;
                var ny = (-k * x - y) / 2;
                x = nx;
                y = ny;
            }
            x -= Math.Max(-2 * size, Math.Min(0, x));
            return -Math.Sqrt(x * x + y * y) * Math.Sign(y) * 0.8;
        }

        private static double HexagonDistance(double x, double y, double radius)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            return Math.Max(x * 0.8660254 + y * 0.5, y) - radius;
        }

        private static double BracketDistance(double x, double y)
        {
            var ax = Math.Abs(x);
            var ay = Math.Abs(y);
            var outer = Math.Max(ax, ay) - 0.9;
            var inner = 0.72 - Math.Max(ax, ay);
            var frame = Math.Max(outer, inner);
            // Seuls les coins du cadre sont gardés.
            var corner = Math.Min(ax, ay) - 0.42;
            return Math.Max(frame, corner);
        }

        // La galaxie derrière les serveurs : un renflement chaud, un disque bleu, des bras
        // en spirale avec des bandes de poussière et des étoiles éparses, sur la même forme
        // que celle où les serveurs sont placés. Sa lumière est sa seule substance : là où la
        // galaxie est sombre l'image est transparente, s'effaçant bien avant
        // ses bords, pour qu'elle repose sur le champ d'étoiles de la carte sans
        // carré autour d'elle. Sur du noir elle ressemble exactement à une image
        // opaque : la couleur est gardée en lumière linéaire une fois prémultipliée.
        public static byte[] GalaxyImage(int size)
        {
            var data = new byte[size * size * 4];
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = (px + 0.5) / size * 2 - 1;
                    var y = (py + 0.5) / size * 2 - 1;
                    var r = Math.Sqrt(x * x + y * y);

                    var noise = Fbm(x * 6, y * 6, 4);
                    var arm = GalaxyShape.ArmStrength(x, y);
                    arm = Math.Pow(arm, 2.2) * (0.55 + 0.9 * noise);
                    var lane = Math.Pow(GalaxyShape.ArmStrength(Rotate(x, y, 0.16).Item1, Rotate(x, y, 0.16).Item2), 6);

                    var disc = Math.Exp(-r / 0.30) * Smooth(1 - Clamp01((r - 0.85) / 0.15));
                    var bulge = Math.Exp(-Math.Pow(r / 0.13, 2));
                    var armLight = arm * disc * 1.6 * Smooth(Clamp01((r - 0.06) / 0.2));
                    var dust = 1 - 0.65 * lane * Smooth(Clamp01((r - 0.12) / 0.2)) * disc * 2.5;
                    dust = Clamp01(dust);

                    var red = (0.10 * disc + 0.35 * armLight) * dust + 1.10 * bulge;
                    var green = (0.14 * disc + 0.55 * armLight) * dust + 0.86 * bulge;
                    var blue = (0.24 * disc + 0.95 * armLight) * dust + 0.55 * bulge;

                    // Des étoiles : quelques pixels doux, plus denses dans les bras, aux
                    // couleurs douces du ciel ; le reste du ciel est
                    // celui de la carte. Assez peu nombreuses pour que les bras se lisent clairement.
                    var h = Hash(px * 7349 + py * 1931);
                    var starChance = (0.0008 + 0.006 * arm) * disc * 3;
                    if (h < starChance)
                    {
                        var star = 0.3 + 0.3 * Hash(px * 13 + py * 9973);
                        byte tr, tg, tb;
                        StarSky.Tint(Hash(px * 211 + py * 7), out tr, out tg, out tb);
                        red += star * tr / 255.0;
                        green += star * tg / 255.0;
                        blue += star * tb / 255.0;
                    }

                    var lr = SrgbToLinear(ToneMap(red));
                    var lg = SrgbToLinear(ToneMap(green));
                    var lb = SrgbToLinear(ToneMap(blue));
                    var edge = Smooth(Clamp01((0.98 - r) / 0.12));
                    // Opaque là où la galaxie est brillante, pour que le ciel
                    // derrière ne transparaisse pas dans son renflement ; jamais moins que
                    // sa lumière, pour que la couleur soit exacte.
                    var light = Math.Max(lr, Math.Max(lg, lb));
                    var alpha = Math.Min(1, 2.5 * Math.Sqrt(light)) * edge;
                    var i = (py * size + px) * 4;
                    if (alpha < 1.0 / 512)
                        continue;
                    var keep = edge / alpha;
                    data[i] = ToByte(LinearToSrgb(lr * keep));
                    data[i + 1] = ToByte(LinearToSrgb(lg * keep));
                    data[i + 2] = ToByte(LinearToSrgb(lb * keep));
                    data[i + 3] = ToByte(alpha);
                }
            }
            return data;
        }

        private static double SrgbToLinear(double c)
        {
            c = Clamp01(c);
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static double LinearToSrgb(double l)
        {
            l = Clamp01(l);
            return l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055;
        }

        private static Tuple<double, double> Rotate(double x, double y, double angle)
        {
            var c = Math.Cos(angle);
            var s = Math.Sin(angle);
            return Tuple.Create(x * c - y * s, x * s + y * c);
        }

        private static double ToneMap(double value)
        {
            return 1 - Math.Exp(-value * 1.4);
        }

        // Un globe vu de loin : une sphère peinte avec les couleurs et le
        // relief échantillonnés sur la vraie planète, éclairée depuis le haut à gauche.
        // colors : largeur x hauteur x 3 flottants (0..1), équirectangulaire, ligne 0 au
        // pôle nord. heights : largeur x hauteur, mètres au-dessus du rayon
        // moyen. viewLongitude : la longitude qui fait face au spectateur, en radians.
        public static byte[] GlobeImage(int size, int width, int height, float[] colors, float[] heights,
            double radius, double viewLongitude, float[] atmosphere)
        {
            var data = new byte[size * size * 4];
            var half = size / 2.0;
            var lx = -0.55;
            var ly = 0.55;
            var lz = 0.63;

            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = (px + 0.5 - half) / (half * 0.94);
                    var y = -(py + 0.5 - half) / (half * 0.94);
                    var r2 = x * x + y * y;
                    var i = (py * size + px) * 4;
                    if (r2 > 1.08)
                        continue;

                    if (r2 > 1)
                    {
                        // Fine lueur de l'atmosphère, quand il y en a une.
                        if (atmosphere != null)
                        {
                            var glow = Clamp01(1 - (Math.Sqrt(r2) - 1) / 0.04) * 0.6;
                            data[i] = ToByte(atmosphere[0]);
                            data[i + 1] = ToByte(atmosphere[1]);
                            data[i + 2] = ToByte(atmosphere[2]);
                            data[i + 3] = ToByte(glow);
                        }
                        continue;
                    }

                    var z = Math.Sqrt(1 - r2);
                    // Dans le repère de la planète : tourner autour de l'axe vertical.
                    var cosL = Math.Cos(viewLongitude);
                    var sinL = Math.Sin(viewLongitude);
                    var wx = x * cosL + z * sinL;
                    var wz = -x * sinL + z * cosL;
                    var lat = Math.Asin(Math.Max(-1, Math.Min(1, y)));
                    var lon = Math.Atan2(wz, wx);

                    var u = (lon / (2 * Math.PI) + 1) % 1.0 * width;
                    var v = (0.5 - lat / Math.PI) * (height - 1);

                    double cr, cg, cb, dhu, dhv;
                    SampleColor(colors, width, height, u, v, out cr, out cg, out cb);
                    dhu = SampleHeight(heights, width, height, u + 0.5, v) - SampleHeight(heights, width, height, u - 0.5, v);
                    dhv = SampleHeight(heights, width, height, u, v + 0.5) - SampleHeight(heights, width, height, u, v - 0.5);

                    // Normale de la sphère, infléchie par le relief.
                    var cosLat = Math.Max(Math.Cos(lat), 0.05);
                    var east = new[] { -Math.Sin(lon), 0.0, Math.Cos(lon) };
                    var north = new[] { -Math.Sin(lat) * Math.Cos(lon), Math.Cos(lat), -Math.Sin(lat) * Math.Sin(lon) };
                    var cellEast = 2 * Math.PI * radius * cosLat / width;
                    var cellNorth = Math.PI * radius / height;
                    var gx = dhu / cellEast * ReliefExaggeration;
                    var gy = -dhv / cellNorth * ReliefExaggeration;
                    var nwx = wx - gx * east[0] - gy * north[0];
                    var nwy = y - gx * east[1] - gy * north[1];
                    var nwz = wz - gx * east[2] - gy * north[2];
                    // Retour au repère de la vue.
                    var nx = nwx * cosL - nwz * sinL;
                    var nz = nwx * sinL + nwz * cosL;
                    var ny = nwy;
                    var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    nx /= length;
                    ny /= length;
                    nz /= length;

                    var light = Math.Max(0, nx * lx + ny * ly + nz * lz);
                    var shade = 0.22 + 0.95 * light;
                    var rim = Math.Pow(1 - z, 3);

                    double red = cr * shade, green = cg * shade, blue = cb * shade;
                    if (atmosphere != null)
                    {
                        red += atmosphere[0] * rim * 0.6;
                        green += atmosphere[1] * rim * 0.6;
                        blue += atmosphere[2] * rim * 0.6;
                    }

                    var edge = Clamp01((1 - Math.Sqrt(r2)) * half * 0.94);
                    data[i] = ToByte(red);
                    data[i + 1] = ToByte(green);
                    data[i + 2] = ToByte(blue);
                    data[i + 3] = ToByte(Math.Max(edge, 0));
                }
            }
            return data;
        }

        // Un globe vu par la caméra propre de la carte, pour quand le jeu ne peut pas
        // le dessiner : chaque pixel d'une image de tout l'écran lance son rayon
        // vers la planète, si bien que le globe tourne, se rapproche et reste sous
        // les repères exactement comme la caméra bouge. Tous les vecteurs sont dans le
        // repère de la planète (y vers son pôle nord, comme pour les échantillons) ; l'œil
        // est compté depuis le centre, en unités du rayon moyen. tanX et tanY :
        // la moitié du champ de vision, en travers et vers le haut. Les pixels hors de la planète restent
        // transparents, l'atmosphère mise à part.
        public static byte[] GlobeViewImage(int width, int height, int columns, int rows, float[] colors, float[] heights,
            double radius, Vec3 eye, Vec3 forward, Vec3 right, Vec3 up, double tanX, double tanY, Vec3 toLight,
            float[] atmosphere)
        {
            var data = new byte[width * height * 4];
            var eyeLength2 = Dot(eye, eye);
            var eyeLength = Math.Sqrt(eyeLength2);
            var lightLength = Math.Max(toLight.Length, 1e-9);
            toLight = toLight * (1 / lightLength);
            radius = Math.Max(radius, 1);

            for (var py = 0; py < height; py++)
            {
                for (var px = 0; px < width; px++)
                {
                    var nx = (px + 0.5) / width * 2 - 1;
                    var ny = 1 - (py + 0.5) / height * 2;
                    var ray = forward + right * (nx * tanX) + up * (ny * tanY);
                    ray = ray * (1 / ray.Length);
                    var i = (py * width + px) * 4;

                    // D'abord sur la sphère moyenne, puis une seconde fois sur la sphère
                    // du sol trouvé là, pour que les montagnes soient où
                    // on les voit depuis les basses altitudes.
                    var sphere = Math.Min(1.0, eyeLength * 0.999);
                    double t;
                    if (!RaySphere(eye, ray, eyeLength2, sphere, out t))
                    {
                        if (atmosphere != null)
                        {
                            var along = -Dot(eye, ray);
                            if (along > 0)
                            {
                                var miss = Math.Sqrt(Math.Max(0, eyeLength2 - along * along));
                                var glow = Clamp01(1 - (miss - 1) / 0.04) * 0.6;
                                if (glow > 0)
                                {
                                    data[i] = ToByte(atmosphere[0]);
                                    data[i + 1] = ToByte(atmosphere[1]);
                                    data[i + 2] = ToByte(atmosphere[2]);
                                    data[i + 3] = ToByte(glow);
                                }
                            }
                        }
                        continue;
                    }

                    double lat, lon, u, v;
                    var hit = eye + ray * t;
                    LatLon(hit, columns, rows, out lat, out lon, out u, out v);
                    var ground = 1 + SampleHeight(heights, columns, rows, u, v) / radius;
                    double t2;
                    if (RaySphere(eye, ray, eyeLength2, Math.Min(ground, eyeLength * 0.999), out t2))
                    {
                        hit = eye + ray * t2;
                        LatLon(hit, columns, rows, out lat, out lon, out u, out v);
                    }
                    var normal0 = hit * (1 / hit.Length);

                    double cr, cg, cb;
                    SampleColor(colors, columns, rows, u, v, out cr, out cg, out cb);
                    var dhu = SampleHeight(heights, columns, rows, u + 0.5, v) - SampleHeight(heights, columns, rows, u - 0.5, v);
                    var dhv = SampleHeight(heights, columns, rows, u, v + 0.5) - SampleHeight(heights, columns, rows, u, v - 0.5);

                    // Normale de la sphère, infléchie par le relief.
                    var cosLat = Math.Max(Math.Cos(lat), 0.05);
                    var east = new Vec3(-Math.Sin(lon), 0, Math.Cos(lon));
                    var north = new Vec3(-Math.Sin(lat) * Math.Cos(lon), Math.Cos(lat), -Math.Sin(lat) * Math.Sin(lon));
                    var cellEast = 2 * Math.PI * radius * cosLat / columns;
                    var cellNorth = Math.PI * radius / rows;
                    var gx = dhu / cellEast * ReliefExaggeration;
                    var gy = -dhv / cellNorth * ReliefExaggeration;
                    var normal = normal0 - east * gx - north * gy;
                    normal = normal * (1 / normal.Length);

                    // Éclairé par le soleil, avec un bord doux vers la nuit.
                    var light = Math.Max(0, Dot(normal, toLight));
                    var day = Smooth(Clamp01((Dot(normal0, toLight) + 0.08) / 0.16));
                    var shade = 0.16 + 1.0 * light * day;
                    var facing = Clamp01(-Dot(ray, normal0));
                    var rim = Math.Pow(1 - facing, 3);

                    double red = cr * shade, green = cg * shade, blue = cb * shade;
                    if (atmosphere != null)
                    {
                        var lit = 0.25 + 0.75 * day;
                        red += atmosphere[0] * rim * 0.6 * lit;
                        green += atmosphere[1] * rim * 0.6 * lit;
                        blue += atmosphere[2] * rim * 0.6 * lit;
                    }

                    data[i] = ToByte(red);
                    data[i + 1] = ToByte(green);
                    data[i + 2] = ToByte(blue);
                    data[i + 3] = 255;
                }
            }
            return data;
        }

        private static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        // Le premier point touché par un rayon de l'œil sur une sphère autour du centre.
        private static bool RaySphere(Vec3 eye, Vec3 ray, double eyeLength2, double sphere, out double t)
        {
            var b = Dot(eye, ray);
            var c = eyeLength2 - sphere * sphere;
            var disc = b * b - c;
            t = 0;
            if (disc < 0)
                return false;
            t = -b - Math.Sqrt(disc);
            return t > 0;
        }

        // Latitude et longitude d'un point, et où il tombe parmi les
        // échantillons (centres de cellules aux nombres entiers).
        private static void LatLon(Vec3 point, int columns, int rows, out double lat, out double lon, out double u, out double v)
        {
            var length = Math.Max(point.Length, 1e-12);
            lat = Math.Asin(Math.Max(-1, Math.Min(1, point.Y / length)));
            lon = Math.Atan2(point.Z, point.X);
            if (lon < 0)
                lon += 2 * Math.PI;
            u = lon / (2 * Math.PI) * columns - 0.5;
            v = (0.5 - lat / Math.PI) * rows - 0.5;
        }

        private static void SampleColor(float[] colors, int width, int height, double u, double v,
            out double r, out double g, out double b)
        {
            int x0, x1, y0, y1;
            double fx, fy;
            Cell(width, height, u, v, out x0, out x1, out y0, out y1, out fx, out fy);
            r = Bilinear(colors, width, x0, x1, y0, y1, fx, fy, 3, 0);
            g = Bilinear(colors, width, x0, x1, y0, y1, fx, fy, 3, 1);
            b = Bilinear(colors, width, x0, x1, y0, y1, fx, fy, 3, 2);
        }

        private static double SampleHeight(float[] heights, int width, int height, double u, double v)
        {
            int x0, x1, y0, y1;
            double fx, fy;
            Cell(width, height, u, v, out x0, out x1, out y0, out y1, out fx, out fy);
            return Bilinear(heights, width, x0, x1, y0, y1, fx, fy, 1, 0);
        }

        private static void Cell(int width, int height, double u, double v,
            out int x0, out int x1, out int y0, out int y1, out double fx, out double fy)
        {
            u = ((u % width) + width) % width;
            v = Math.Max(0, Math.Min(height - 1, v));
            x0 = (int)Math.Floor(u);
            fx = u - x0;
            x0 %= width;
            x1 = (x0 + 1) % width;
            y0 = (int)Math.Floor(v);
            fy = v - y0;
            y1 = Math.Min(height - 1, y0 + 1);
        }

        private static double Bilinear(float[] values, int width, int x0, int x1, int y0, int y1,
            double fx, double fy, int stride, int offset)
        {
            var a = values[(y0 * width + x0) * stride + offset];
            var b = values[(y0 * width + x1) * stride + offset];
            var c = values[(y1 * width + x0) * stride + offset];
            var d = values[(y1 * width + x1) * stride + offset];
            return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
        }

        // Une étoile vue depuis une planète : un disque blanc-jaune avec un large halo.
        public static byte[] SunImage(int size)
        {
            var data = new byte[size * size * 4];
            var half = size / 2.0;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var x = (px + 0.5 - half) / half;
                    var y = (py + 0.5 - half) / half;
                    var r = Math.Sqrt(x * x + y * y);
                    var core = Clamp01((0.36 - r) / 0.03);
                    var halo = Math.Pow(Clamp01(1 - r), 3) * 0.85;
                    var a = Math.Max(core, halo);
                    var i = (py * size + px) * 4;
                    data[i] = 255;
                    data[i + 1] = ToByte(0.93 - 0.1 * (1 - core));
                    data[i + 2] = ToByte(0.75 - 0.25 * (1 - core));
                    data[i + 3] = ToByte(a);
                }
            }
            return data;
        }

        private static double Fbm(double x, double y, int octaves)
        {
            double sum = 0, amplitude = 0.5, total = 0;
            for (var o = 0; o < octaves; o++)
            {
                sum += amplitude * ValueNoise(x, y);
                total += amplitude;
                x *= 2.03;
                y *= 2.03;
                amplitude *= 0.5;
            }
            return sum / total;
        }

        private static double ValueNoise(double x, double y)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var fx = Smooth(x - x0);
            var fy = Smooth(y - y0);
            var a = Hash(x0 * 374761 + y0 * 668265);
            var b = Hash((x0 + 1) * 374761 + y0 * 668265);
            var c = Hash(x0 * 374761 + (y0 + 1) * 668265);
            var d = Hash((x0 + 1) * 374761 + (y0 + 1) * 668265);
            return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
        }

        private static double Hash(int n)
        {
            unchecked
            {
                var h = (uint)n;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                h *= 0x846ca68b;
                h ^= h >> 16;
                return h / 4294967296.0;
            }
        }

        private static double Smooth(double t)
        {
            t = Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        private static double Clamp01(double v)
        {
            return v < 0 ? 0 : (v > 1 ? 1 : v);
        }

        private static byte ToByte(double v)
        {
            return (byte)Math.Round(Clamp01(v) * 255);
        }
    }
}
