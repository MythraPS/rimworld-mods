// Handwoven Rugs: the "Rug pictures" setting takes effect at once, no restart.
// The setting (Mod Settings Framework radio buttons, end of Patches/IranianRugs.xml) changes the rug XML at game start.
// While the settings window is open, this watches which option is picked; when it changes, it applies the matching
// RugLookDef (Defs/RugLook.xml, both written by Source/make_look.py): new picture folders and descriptions for the
// rugs and the Ideology rug styles. Then it clears the cached pictures and redraws every rug on every map.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HandwovenRugs
{
    public class RugLookEntry
    {
        public string def;
        public string texPath;
        public string description;
    }

    public class RugLookDef : Def
    {
        public List<RugLookEntry> things = new List<RugLookEntry>();
        public List<RugLookEntry> styles = new List<RugLookEntry>();
    }

    public static class RugLook
    {
        const string SettingId = "IranianRugs_Look";
        static string current;                                   // option id now applied; null until the window first draws

        static FieldInfo idField, optionsField, defaultField;
        static MethodInfo enabledMethod;
        static readonly FieldInfo cachedGraphic = AccessTools.Field(typeof(GraphicData), "cachedGraphic");
        static readonly FieldInfo styleGraphic = AccessTools.Field(typeof(ThingStyleDef), "graphic");
        static readonly MethodInfo styleIcon = AccessTools.Method(typeof(ThingStyleDef), "ResolveUIIcon");
        static readonly MethodInfo thingIcon = AccessTools.Method(typeof(ThingDef), "ResolveIcon");
        static readonly FieldInfo detailedDesc = AccessTools.Field(typeof(ThingDef), "descriptionDetailedCached");
        static readonly FieldInfo minifiedGraphic = AccessTools.Field(typeof(MinifiedThing), "cachedGraphic");

        public static void Patch(Harmony harmony)
        {
            var radio = AccessTools.TypeByName("ModSettingsFramework.PatchOperationRadioButtons");
            var option = AccessTools.TypeByName("ModSettingsFramework.PatchOperationModOption");
            var container = AccessTools.TypeByName("ModSettingsFramework.ModSettingsContainer");
            if (radio == null || option == null || container == null)
            {
                Log.Warning("[Handwoven Rugs] Mod Settings Framework not found; the rug pictures setting will need a restart.");
                return;
            }
            idField = AccessTools.Field(radio, "id");
            optionsField = AccessTools.Field(radio, "options");
            defaultField = AccessTools.Field(option, "defaultValue");
            enabledMethod = AccessTools.Method(container, "PatchOperationEnabled", new[] { typeof(string), typeof(bool) });
            var target = AccessTools.Method(radio, "DoSettings");
            if (idField == null || optionsField == null || defaultField == null || enabledMethod == null || target == null)
            {
                Log.Warning("[Handwoven Rugs] Mod Settings Framework has changed; the rug pictures setting will need a restart.");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(RugLook), nameof(Postfix)));
        }

        static void Postfix(object __instance, object container)
        {
            try
            {
                if ((string)idField.GetValue(__instance) != SettingId) return;
                string picked = null;
                foreach (var opt in (IEnumerable)optionsField.GetValue(__instance))
                {
                    var id = (string)idField.GetValue(opt);
                    if ((bool)enabledMethod.Invoke(container, new object[] { id, defaultField.GetValue(opt) })) { picked = id; break; }
                }
                if (picked == null) return;
                if (current == null) { current = picked; return; }  // what the game started with, already applied
                if (picked == current) return;
                current = picked;
                Apply(DefDatabase<RugLookDef>.GetNamedSilentFail(picked));
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Handwoven Rugs] could not switch the rug pictures: " + e, 55117730);
            }
        }

        static void Apply(RugLookDef look)
        {
            if (look == null) return;
            var changed = new HashSet<ThingDef>();
            foreach (var e in look.things)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(e.def);
                if (def?.graphicData == null) continue;
                def.graphicData.texPath = e.texPath;
                cachedGraphic.SetValue(def.graphicData, null);
                def.graphic = def.graphicData.Graphic;
                thingIcon?.Invoke(def, null);
                if (!e.description.NullOrEmpty()) def.description = e.description;
                detailedDesc?.SetValue(def, null);
                changed.Add(def);
            }
            foreach (var e in look.styles)
            {
                var style = DefDatabase<ThingStyleDef>.GetNamedSilentFail(e.def);
                if (style?.graphicData == null) continue;
                style.graphicData.texPath = e.texPath;
                cachedGraphic.SetValue(style.graphicData, null);
                if (style.blueprintGraphicData != null)
                {
                    style.blueprintGraphicData.texPath = e.texPath;
                    cachedGraphic.SetValue(style.blueprintGraphicData, null);
                }
                styleGraphic.SetValue(style, style.graphicData.Graphic);
                styleIcon?.Invoke(style, null);
            }
            if (Current.Game == null) return;
            foreach (var map in Find.Maps)
            {
                foreach (var def in changed)
                    foreach (var t in map.listerThings.ThingsOfDef(def))
                        t.Notify_ColorChanged();
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
                    if (t is MinifiedThing m && m.InnerThing != null && changed.Contains(m.InnerThing.def))
                    {
                        m.InnerThing.Notify_ColorChanged();
                        minifiedGraphic?.SetValue(m, null);
                    }
            }
        }
    }
}
