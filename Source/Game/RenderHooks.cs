using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using VRageMath;
using VRageRender;

namespace SirHolomap
{
    // While the map is open, the game keeps drawing its own world, from the
    // map's camera: the real planet, its relief and colours, the sun, the
    // grids and the sky, all by the game's own renderer. The character and
    // the game's camera never move: only what is handed to the renderer
    // changes, just before it leaves the game thread.
    //
    // Two prefixes, on the two calls the game makes every frame
    // (MyGuiScreenGamePlay.Draw): MyRenderProxy.SetCameraViewMatrix, whose
    // view and position become the map's, and
    // MyRenderProxy.UpdateRenderEnvironment, whose sun is turned towards the
    // globe when the player wants it lit from the camera. The voxel level of
    // detail follows the render camera, so the ground sharpens as the map
    // zooms in.
    internal static class RenderHooks
    {
        public const string CameraStep = "MyRenderProxy.SetCameraViewMatrix";
        public const string LightStep = "MyRenderProxy.UpdateRenderEnvironment";

        // Written on the game thread by the map, read on the game thread by
        // the prefixes: no locking needed.
        public static bool Active;
        public static MatrixD View = MatrixD.Identity;
        public static Vector3D Position;
        public static double NeededFar;
        public static bool Jump;
        public static bool Daylight;
        public static Vector3 LightDirection = Vector3.Forward;

        // What the game itself last asked for.
        public static float LastFov = MathHelper.ToRadians(70);
        public static float LastAspect = 16f / 9f;
        public static Vector3 GameLightDirection = Vector3.Forward;

        // False when another plugin holds one of the steps: the map then
        // opens without the 3D view.
        public static bool CameraAvailable;
        public static bool LightAvailable;

        public static MethodInfo CameraMethod
        {
            get { return typeof(MyRenderProxy).GetMethod("SetCameraViewMatrix", BindingFlags.Public | BindingFlags.Static); }
        }

        public static MethodInfo LightMethod
        {
            get { return typeof(MyRenderProxy).GetMethod("UpdateRenderEnvironment", BindingFlags.Public | BindingFlags.Static); }
        }

        // Owners of a step other than us.
        public static List<string> OtherOwners(MethodBase method, string ownId)
        {
            var others = new List<string>();
            if (method == null)
                return others;
            var info = Harmony.GetPatchInfo(method);
            if (info == null)
                return others;
            foreach (var owner in info.Owners)
            {
                if (!string.IsNullOrEmpty(owner) && owner != ownId && !others.Contains(owner))
                    others.Add(owner);
            }
            return others;
        }

        public static bool CameraPrefix(ref MatrixD viewMatrix, ref Matrix projectionMatrix, ref Matrix projectionFarMatrix,
            float fov, float nearPlane, ref float farPlane, ref float farFarPlane, ref Vector3D cameraPosition, ref bool smooth)
        {
            try
            {
                if (fov > 0.01f)
                    LastFov = fov;
                if (Math.Abs(projectionMatrix.M11) > 1e-6f)
                    LastAspect = projectionMatrix.M22 / projectionMatrix.M11;

                if (!Active || !CameraAvailable)
                    return true;

                viewMatrix = View;
                cameraPosition = Position;
                if (Jump)
                {
                    smooth = false;
                    Jump = false;
                }

                // Planets and grids farther than the game's planes would be
                // culled: the planes follow the map's camera.
                var near = Math.Min(4f, Math.Max(nearPlane, 0.01f));
                var needed = (float)Math.Min(NeededFar, 1e9);
                if (needed > farPlane)
                {
                    farPlane = Math.Min(needed, 2e6f);
                    projectionMatrix = Matrix.CreatePerspectiveFieldOfView(fov, LastAspect, near, farPlane);
                }
                if (needed > farFarPlane)
                {
                    farFarPlane = needed;
                    projectionFarMatrix = Matrix.CreatePerspectiveFieldOfView(fov, LastAspect, near, farFarPlane);
                }
            }
            catch (Exception)
            {
                // Never break the game's frame.
            }
            return true;
        }

        public static bool LightPrefix(ref MyEnvironmentData data)
        {
            try
            {
                GameLightDirection = data.EnvironmentLight.SunLightDirection;
                if (Active && Daylight && LightAvailable)
                    data.EnvironmentLight.SunLightDirection = LightDirection;
            }
            catch (Exception)
            {
            }
            return true;
        }

        public static void Release()
        {
            Active = false;
            Jump = false;
            Daylight = false;
        }
    }
}
