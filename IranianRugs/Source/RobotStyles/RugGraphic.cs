// Handwoven Rugs: the "Both" rug-pictures setting. The game's Graphic_Random finds textures in subfolders but then loads
// each one as "<folder>/<name>", dropping the subfolder, so a folder holding Photo/ and Drawn/ shows red X squares.
// This picker reads Photo/ and Drawn/ separately and puts both sets in one list. A folder without those subfolders
// (the Photos-only and Drawn-only settings point straight at one) loads the normal way.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HandwovenRugs
{
    public class Graphic_RugFolders : Graphic_Random
    {
        static readonly string[] Subfolders = { "Photo", "Drawn" };

        public override void Init(GraphicRequest req)
        {
            var all = new List<Graphic>();
            foreach (var sub in Subfolders)
            {
                var part = req;
                part.path = req.path + "/" + sub;
                if (!ContentFinder<Texture2D>.GetAllInFolder(part.path).Any()) continue;
                base.Init(part);
                all.AddRange(subGraphics);
            }
            if (all.Count == 0)
            {
                base.Init(req);
                return;
            }
            path = req.path;
            subGraphics = all.ToArray();
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            if (newColorTwo != Color.white)
                Log.ErrorOnce("Cannot use Graphic_RugFolders.GetColoredVersion with a non-white colorTwo.", 9910251);
            return GraphicDatabase.Get<Graphic_RugFolders>(path, newShader, drawSize, newColor, Color.white, data);
        }
    }
}
