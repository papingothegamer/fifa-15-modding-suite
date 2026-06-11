using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FIFA15.AccessoryManager
{
    public class Player
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public int ShoeId { get; set; }
        public int GkGloveId { get; set; }
        public int AnkleTapeId { get; set; }
        public string TeamName { get; set; }
    }

    public class CsvDatabaseReader
    {
        public static List<Player> LoadPlayers(string csvPath)
        {
            var players = new List<Player>();

            if (!File.Exists(csvPath))
                return players;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0) return players;

            var headers = lines[0].Split(',').Select(h => h.Trim().ToLower()).ToList();
            
            int idIndex = headers.IndexOf("playerid");
            int nameIndex = headers.IndexOf("name");
            if (nameIndex == -1) nameIndex = headers.IndexOf("playername");
            
            int shoeIndex = headers.IndexOf("shoeid");
            if (shoeIndex == -1) shoeIndex = headers.IndexOf("shoetypecode"); // Sometimes called shoetypecode
            
            int gloveIndex = headers.IndexOf("gkgloveid");
            if (gloveIndex == -1) gloveIndex = headers.IndexOf("gkglovetypecode");

            if (idIndex == -1)
                throw new Exception("CSV must contain 'playerid' column.");

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length <= idIndex) continue;

                var player = new Player();
                if (int.TryParse(parts[idIndex], out int pid)) player.PlayerId = pid;
                
                if (nameIndex != -1 && parts.Length > nameIndex)
                    player.Name = parts[nameIndex];
                else
                    player.Name = "Unknown Player " + pid;

                if (shoeIndex != -1 && parts.Length > shoeIndex && int.TryParse(parts[shoeIndex], out int sid))
                    player.ShoeId = sid;

                if (gloveIndex != -1 && parts.Length > gloveIndex && int.TryParse(parts[gloveIndex], out int gid))
                    player.GkGloveId = gid;

                players.Add(player);
            }

            return players;
        }
    }
}
