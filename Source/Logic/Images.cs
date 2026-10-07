using System;

namespace SirHolomap
{
    // Pictures the map paints itself, as RGBA bytes (straight alpha, sRGB):
    // marker shapes, the galaxy, the globes of the system view. Nothing is
    // shipped with the plugin; everything is computed on the player's machine.
    public static class Images
    {
        public const int ShapeSize = 64;

        // Slopes of the relief are steepened this much on the small globes,
        // so that mountain ranges read at a glance.
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
        }

        // White shapes, tinted when drawn.
        public static byte[] ShapeImage(Shape shape)
        {
            var size = ShapeSize;
            var data = new byte[size * size * 4];
            var half = size / 2.0;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    // Pixel centre, in units where the shape spans [-1, 1].
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
            }
            return 0;
        }

        private static double Edge(double distance, double pixel)
        {
            return Clamp01(0.5 - distance / (pixel * 1.5));
        }

        // An upward pointing triangle (pointing to -y on screen).
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
            // Only the corners of the frame are kept.
            var corner = Math.Min(ax, ay) - 0.42;
            return Math.Max(frame, corner);
        }

        // The galaxy behind the servers: a warm bulge, a blue disc, spiral
        // arms with dust lanes and scattered stars, after the same shape the
        // servers are placed on.
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

                    // Stars: rare bright pixels, denser in the arms.
                    var h = Hash(px * 7349 + py * 1931);
                    var starChance = 0.0025 + 0.02 * arm * disc;
                    if (h < starChance)
                    {
                        var star = 0.6 + 0.4 * Hash(px * 13 + py * 9973);
                        red += star;
                        green += star;
                        blue += star * 1.05;
                    }

                    var i = (py * size + px) * 4;
                    data[i] = ToByte(ToneMap(red));
                    data[i + 1] = ToByte(ToneMap(green));
                    data[i + 2] = ToByte(ToneMap(blue));
                    data[i + 3] = 255;
                }
            }
            return data;
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

        // A globe seen from far: a sphere painted with the colours and the
        // relief sampled on the real planet, lit from the upper left.
        // colors: width x height x 3 floats (0..1), equirectangular, row 0 at
        // the north pole. heights: width x height, metres above the mean
        // radius. viewLongitude: the longitude facing the viewer, radians.
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
                        // Thin glow of the atmosphere, when there is one.
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
                    // Into the planet's frame: turn around the vertical axis.
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

                    // Normal of the sphere, bent by the relief.
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
                    // Back to the view frame.
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

        // A globe seen through the map's own camera, for when the game cannot
        // draw it: every pixel of a picture of the whole screen casts its ray
        // at the planet, so that the globe turns, comes closer and stays under
        // the markers exactly as the camera moves. All vectors are in the
        // planet's frame (y towards its north pole, as for the samples); the
        // eye is from the centre, in units of the mean radius. tanX and tanY:
        // half the field of view, across and up. Pixels off the planet stay
        // transparent, the atmosphere aside.
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

                    // First on the mean sphere, then once more on the sphere
                    // of the ground found there, so that mountains sit where
                    // they are seen from low altitudes.
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

                    // Normal of the sphere, bent by the relief.
                    var cosLat = Math.Max(Math.Cos(lat), 0.05);
                    var east = new Vec3(-Math.Sin(lon), 0, Math.Cos(lon));
                    var north = new Vec3(-Math.Sin(lat) * Math.Cos(lon), Math.Cos(lat), -Math.Sin(lat) * Math.Sin(lon));
                    var cellEast = 2 * Math.PI * radius * cosLat / columns;
                    var cellNorth = Math.PI * radius / rows;
                    var gx = dhu / cellEast * ReliefExaggeration;
                    var gy = -dhv / cellNorth * ReliefExaggeration;
                    var normal = normal0 - east * gx - north * gy;
                    normal = normal * (1 / normal.Length);

                    // Lit by the sun, with a soft edge to the night.
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

        // The nearest hit of a ray from the eye on a sphere around the centre.
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

        // Latitude and longitude of a point, and where it falls among the
        // samples (cell centres at whole numbers).
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

        // A star seen from a planet: a white-yellow disc with a wide halo.
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
