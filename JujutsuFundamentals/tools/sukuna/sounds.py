"""Writes 1.6/Defs/SoundDefs/Sounds.xml: every technique's sound, layered from vanilla clips at
new pitches (no audio files shipped; idea from "Rim All-Stars", her call 2026-11-07).
Royalty clips are always there (VPE needs Royalty); Biotech/Anomaly layers carry MayRequire, so
without that DLC the layer is simply left out.

  python tools/sounds.py            (run from the mod folder; the Sukuna mod has its own copy)
"""
import sys
from pathlib import Path

FOLDER, CLIP = "AudioGrain_Folder", "AudioGrain_Clip"
DLC = {"Biotech": "Ludeon.RimWorld.Biotech", "Anomaly": "Ludeon.RimWorld.Anomaly"}

# name -> (clip path, grain kind, dlc or None)
C = {
    "skip_entry": ("Misc/Psycasts/Skip/Entry", FOLDER, None),
    "skip_exit": ("Misc/Psycasts/Skip/Exit", FOLDER, None),
    "skip_pulse": ("Misc/Psycasts/Skip/Pulse", FOLDER, None),
    "pulser": ("Misc/Artifacts/Psychic_Animal_Pulser", FOLDER, None),
    "thump": ("Impact/ThumpCannon", FOLDER, None),
    "psy_pulse": ("Misc/Psycasts/Psycast_Psychic_Pulse", FOLDER, None),
    "shockwave": ("Explosion/Mechband_Shockwave_Explosion_01a", CLIP, "Biotech"),
    "quake_start": ("Misc/Psycasts/Neuroquake/Psycast_Neuroquake_Cast_Start_01a", CLIP, None),
    "quake_apply": ("Misc/Psycasts/Neuroquake/Psycast_Neuroquake_Apply_01f", CLIP, None),
    "giant": ("Explosion/GiantExplosion", FOLDER, None),
    "void_boom": ("Building/VoidMonolith/Activation/L2L3/VM_Act_L2L3_Boom_A", CLIP, "Anomaly"),
    "void_boom2": ("Building/VoidMonolith/Activation/L1L2/VM_Act_L1L2_Boom_A", CLIP, "Anomaly"),
    "void_terror": ("Pawn/Abilities/VoidTerror/Void_Terror_Cast_A", CLIP, "Anomaly"),
    "zeus": ("Impact/BladelinkZeusHammer", FOLDER, None),
    "zap": ("Building/VoidMonolith/Activation/Zap", FOLDER, "Anomaly"),
    "punch": ("Impact/PunchHitPawn", FOLDER, None),
    "mono": ("Impact/MonoSword", FOLDER, None),
    "cut": ("Impact/MeleeHit_Metal_Sharp", FOLDER, None),
    "mono_bl": ("Impact/BladelinkMonoSword", FOLDER, None),
    "plasma_bl": ("Impact/BladelinkPlasmaSword", FOLDER, None),
    "fire_resolve": ("Pawn/Abilities/FireSpew/Resolve", FOLDER, None),
    "fire_warm": ("Pawn/Abilities/FireBurst_Warmup", CLIP, "Biotech"),
    "bloodfeed": ("Pawn/Abilities/Bloodfeed", FOLDER, "Biotech"),
    "flesh": ("Impact/Bullet_Flesh", FOLDER, None),
    "vaporize": ("Explosion/Vaporize", FOLDER, None),
}

# sound -> [(clip, volume, pitch, delay seconds)]
GOJO = {
    "Gojo_Snd_BlueCast": [("skip_entry", 30, 0.6, 0), ("pulser", 22, 0.5, 0)],
    "Gojo_Snd_BlueLand": [("vaporize", 30, 0.5, 0), ("pulser", 25, 0.4, 0), ("skip_pulse", 25, 0.45, 0.05)],
    "Gojo_Snd_RedCast": [("thump", 35, 1.1, 0), ("psy_pulse", 20, 1.4, 0)],
    "Gojo_Snd_RedBurst": [("giant", 30, 1.3, 0), ("shockwave", 30, 1.2, 0), ("thump", 25, 0.8, 0)],
    "Gojo_Snd_PurpleCharge": [("quake_start", 30, 0.8, 0), ("pulser", 15, 0.35, 0.2)],
    "Gojo_Snd_PurpleFire": [("quake_apply", 35, 0.7, 0), ("giant", 30, 0.6, 0.05), ("void_boom", 30, 0.8, 0)],
    "Gojo_Snd_VoidOpen": [("void_boom", 40, 0.7, 0), ("void_terror", 30, 0.8, 0.1), ("quake_apply", 30, 0.5, 0), ("pulser", 20, 0.3, 0)],
    "Gojo_Snd_VoidStart": [("quake_start", 25, 0.6, 0)],
    "Gojo_Snd_Teleport": [("skip_exit", 30, 1.2, 0)],
    "Gojo_Snd_Pull": [("skip_pulse", 30, 0.8, 0), ("pulser", 15, 0.7, 0)],
    "Gojo_Snd_Push": [("thump", 30, 1.4, 0), ("psy_pulse", 18, 1.2, 0)],
    "Gojo_Snd_BlackFlash": [("zeus", 40, 0.7, 0), ("punch", 35, 0.6, 0), ("zap", 30, 0.6, 0), ("psy_pulse", 18, 1.8, 0.03)],
}
SUKUNA = {
    "Sukuna_Snd_Dismantle": [("mono_bl", 22, 1.5, 0)],
    "Sukuna_Snd_Cleave": [("mono", 30, 1.3, 0), ("cut", 25, 0.9, 0.04), ("mono", 22, 1.6, 0.09)],
    "Sukuna_Snd_ShrineStart": [("quake_start", 28, 0.55, 0)],
    "Sukuna_Snd_ShrineOpen": [("void_boom2", 40, 0.6, 0), ("void_terror", 30, 0.8, 0.1), ("quake_apply", 30, 0.6, 0), ("giant", 18, 0.4, 0.05)],
    "Sukuna_Snd_FireArrowDraw": [("fire_warm", 30, 0.8, 0), ("quake_start", 18, 1.3, 0)],
    "Sukuna_Snd_FireArrowLoose": [("fire_resolve", 35, 0.7, 0), ("thump", 25, 0.6, 0)],
    "Sukuna_Snd_WorldSlashStart": [("quake_start", 25, 0.7, 0)],
    "Sukuna_Snd_WorldSlash": [("plasma_bl", 40, 0.45, 0), ("shockwave", 30, 0.6, 0.02), ("giant", 18, 1.5, 0.05)],
    "Sukuna_Snd_AirStep": [("skip_exit", 18, 1.5, 0)],
    "Sukuna_Snd_Benevolent": [("quake_apply", 30, 1.1, 0), ("psy_pulse", 25, 0.7, 0)],
    "Sukuna_Snd_PiercingBlood": [("bloodfeed", 30, 1.6, 0), ("flesh", 25, 0.8, 0), ("thump", 18, 1.8, 0)],
    "Sukuna_Snd_BlackFlash": [("zeus", 40, 0.7, 0), ("punch", 35, 0.6, 0), ("zap", 30, 0.6, 0), ("psy_pulse", 18, 1.8, 0.03)],
}


def sound(name, layers):
    out = [f"  <SoundDef>\n    <defName>{name}</defName>\n    <context>MapOnly</context>\n    <maxSimultaneous>4</maxSimultaneous>\n    <subSounds>"]
    for clip, vol, pitch, delay in layers:
        path, kind, dlc = C[clip]
        tag = "clipFolderPath" if kind == FOLDER else "clipPath"
        req = f' MayRequire="{DLC[dlc]}"' if dlc else ""
        lo, hi = round(pitch * 0.95, 3), round(pitch * 1.05, 3)
        out.append(f"""      <li{req}>
        <grains>
          <li Class="{kind}">
            <{tag}>{path}</{tag}>
          </li>
        </grains>
        <volumeRange>{vol}~{vol}</volumeRange>
        <pitchRange>{lo}~{hi}</pitchRange>
      </li>""")   # (no startDelayRange: the game allows it only on looping sounds)
    out.append("    </subSounds>\n  </SoundDef>\n")
    return "\n".join(out)


def main():
    mod = Path.cwd()
    table = SUKUNA if "Sukuna" in mod.name else GOJO
    d = mod / "1.6/Defs/SoundDefs"
    d.mkdir(parents=True, exist_ok=True)
    body = "".join(sound(n, l) for n, l in table.items())
    (d / "Sounds.xml").write_text('<?xml version="1.0" encoding="utf-8"?>\n<!-- made by tools/sounds.py - edit there -->\n<Defs>\n\n' + body + "</Defs>\n")
    print(f"{len(table)} sounds -> {d / 'Sounds.xml'}")


if __name__ == "__main__":
    main()
