using System.Reflection;
using HarmonyLib;
using Verse;

namespace SukunaPsycasts
{
    [StaticConstructorOnStartup]
    public static class SukunaStartup
    {
        static SukunaStartup()
        {
            var harmony = new Harmony("mythraps.ryomensukuna");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            StatPart_Sorcerer.Register();
            Log.Message("[Ryomen Sukuna] Harmony patches applied.");
        }
    }
}
