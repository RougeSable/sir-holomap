using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using VRageMath;
using VRageRender;

namespace SirHolomap
{
    // Pendant que la carte est ouverte, le jeu continue de dessiner son propre monde, depuis la
    // caméra de la carte : la vraie planète, son relief et ses couleurs, le soleil, les
    // grilles et le ciel, le tout par le moteur de rendu du jeu. Le personnage et
    // la caméra du jeu ne bougent jamais : seul ce qui est remis au moteur de rendu
    // change, juste avant de quitter le fil du jeu.
    //
    // Deux préfixes, sur les deux appels que le jeu fait à chaque image
    // (MyGuiScreenGamePlay.Draw) : MyRenderProxy.SetCameraViewMatrix, dont la
    // vue et la position deviennent celles de la carte, et
    // MyRenderProxy.UpdateRenderEnvironment, dont le soleil est tourné vers le
    // globe quand le joueur le veut éclairé depuis la caméra. Le niveau de détail
    // des voxels suit la caméra de rendu, si bien que le sol s'affine quand la carte
    // zoome.
    internal static class RenderHooks
    {
        public const string CameraStep = "MyRenderProxy.SetCameraViewMatrix";
        public const string LightStep = "MyRenderProxy.UpdateRenderEnvironment";

        // Écrit sur le fil du jeu par la carte, lu sur le fil du jeu par
        // les préfixes : aucun verrou nécessaire.
        public static bool Active;
        public static MatrixD View = MatrixD.Identity;
        public static Vector3D Position;
        public static double NeededFar;
        public static bool Jump;
        public static bool Daylight;
        public static Vector3 LightDirection = Vector3.Forward;

        // Ce que le jeu lui-même a demandé en dernier.
        public static float LastFov = MathHelper.ToRadians(70);
        public static float LastAspect = 16f / 9f;
        public static Vector3 GameLightDirection = Vector3.Forward;

        // Faux quand un autre greffon tient l'une des étapes : la carte
        // s'ouvre alors sans la vue 3D.
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

        // Les propriétaires d'une étape autres que nous.
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

                // Les planètes et les grilles plus loin que les plans du jeu seraient
                // éliminées : les plans suivent la caméra de la carte.
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("RenderHooks.CameraPrefix", ex);
                // Ne jamais casser l'image du jeu.
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
            catch (Exception ex)
            {
                HolomapPlugin.Caught("RenderHooks.LightPrefix", ex);
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
