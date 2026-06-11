using System;
using System.Collections.Generic;
using System.IO;
using FifaLibrary;

namespace FIFA15.AccessoryManager
{
    public class NativeDbReader
    {
        public static List<Player> LoadPlayers(string dbPath, string xmlPath, string langDbPath, string langXmlPath)
        {
            var players = new List<Player>();

            if (!File.Exists(dbPath) || !File.Exists(xmlPath))
                throw new FileNotFoundException("Could not find fifa_ng_db.db or fifa_ng_db-meta.xml");

            if (!File.Exists(langDbPath) || !File.Exists(langXmlPath))
                throw new FileNotFoundException("Could not find eng_us.db or eng_us-meta.xml");

            // Load main DB
            DbFile dbFile = new DbFile(dbPath, xmlPath);
            Table playersTable = dbFile.Table[dbFile.GetTableIndex("players")];
            Table teamsTable = dbFile.Table[dbFile.GetTableIndex("teams")];
            Table linksTable = dbFile.Table[dbFile.GetTableIndex("teamplayerlinks")];
            Table namesTable = dbFile.Table[dbFile.GetTableIndex("playernames")];

            // Load language DB
            DbFile langDbFile = new DbFile(langDbPath, langXmlPath);
            Table langTable = langDbFile.Table[langDbFile.GetTableIndex("LanguageStrings")];
            
            // Build Language Dictionary
            var langDict = new Dictionary<int, string>();
            if (langTable.Records != null)
            {
                foreach (Record r in langTable.Records)
                {
                    int hash = r.GetIntField("hashid");
                    if (!langDict.ContainsKey(hash))
                    {
                        langDict[hash] = r.GetStringField("sourcetext");
                    }
                }
            }

            // Build Player Name Dictionary
            var playerNames = new Dictionary<int, string>();
            if (namesTable.Records != null)
            {
                foreach (Record r in namesTable.Records)
                {
                    int nid = r.GetIntField("nameid");
                    if (!playerNames.ContainsKey(nid))
                    {
                        playerNames[nid] = r.GetStringField("name");
                    }
                }
            }

            // Build Team ID -> Team Name mapping
            var teamNames = new Dictionary<int, string>();
            foreach (Record record in teamsTable.Records)
            {
                int tid = record.GetIntField("teamid");
                
                // FIFA 15 generates the team name string hash using "TeamName_{teamid}"
                int tnid = unchecked((int)FifaUtil.ComputeLanguageHash("TeamName_" + tid));
                
                string tName = langDict.ContainsKey(tnid) ? langDict[tnid] : "";
                if (string.IsNullOrWhiteSpace(tName)) tName = "Unknown Team " + tid;
                teamNames[tid] = tName;
            }

            // Build Player ID -> Team ID mapping
            var playerTeams = new Dictionary<int, int>();
            foreach (Record record in linksTable.Records)
            {
                int pid = record.GetIntField("playerid");
                int tid = record.GetIntField("teamid");
                // Only take the first team assigned if multiple exist
                if (!playerTeams.ContainsKey(pid))
                {
                    playerTeams[pid] = tid;
                }
            }

            foreach (Record record in playersTable.Records)
            {
                int pid = record.GetIntField("playerid");
                
                // We only care about players assigned to a team
                if (!playerTeams.ContainsKey(pid)) continue;

                int tid = playerTeams[pid];
                string teamName = teamNames.ContainsKey(tid) ? teamNames[tid] : "Free Agents";

                int shoe = record.GetIntField("shoetypecode");
                int glove = record.GetIntField("gkglovetypecode");

                // Check all 4 accessory slots for Ankle Tape (ID = 14)
                int tapeColor = -1; // -1 represents not assigned
                int[] codes = {
                    record.GetIntField("accessorycode1"),
                    record.GetIntField("accessorycode2"),
                    record.GetIntField("accessorycode3"),
                    record.GetIntField("accessorycode4")
                };
                int[] colors = {
                    record.GetIntField("accessorycolor1"),
                    record.GetIntField("accessorycolor2"),
                    record.GetIntField("accessorycolor3"),
                    record.GetIntField("accessorycolor4")
                };

                for(int i = 0; i < 4; i++) {
                    if (codes[i] == 14) {
                        tapeColor = colors[i];
                        break;
                    }
                }

                int fnId = record.GetIntField("firstnameid");
                int lnId = record.GetIntField("lastnameid");
                int cnId = record.GetIntField("commonnameid");

                string firstName = playerNames.ContainsKey(fnId) ? playerNames[fnId] : "";
                string lastName = playerNames.ContainsKey(lnId) ? playerNames[lnId] : "";
                string commonName = playerNames.ContainsKey(cnId) ? playerNames[cnId] : "";

                string displayName = "";
                if (!string.IsNullOrWhiteSpace(commonName) && commonName != " ")
                    displayName = commonName;
                else if (!string.IsNullOrWhiteSpace(firstName) && firstName != " ")
                    displayName = firstName + " " + lastName;
                else
                    displayName = lastName;

                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = "Unknown Player " + pid;

                var p = new Player
                {
                    PlayerId = pid,
                    Name = displayName.Trim(),
                    ShoeId = shoe,
                    GkGloveId = glove,
                    AnkleTapeId = tapeColor,
                    TeamName = teamName
                };

                players.Add(p);
            }

            return players;
        }
    }
}
