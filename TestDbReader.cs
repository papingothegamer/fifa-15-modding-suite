using System;
using System.Reflection;
using System.IO;

class Program
{
    static void Main()
    {
        try
        {
            var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Fifa Master\DB Master\FifaLibrary14.dll");
            var dbFileType = asm.GetType("FifaLibrary.DbFile");
            var langType = asm.GetType("FifaLibrary.Language");

            // Assuming constructor is DbFile(string dbName, string xmlName)
            Console.WriteLine("Instantiating DbFile...");
            object dbFile = Activator.CreateInstance(dbFileType, new object[] { @"C:\Users\Laptop\Downloads\big-bh test\backup\db\fifa_ng_db.db", @"C:\Users\Laptop\Downloads\big-bh test\backup\db\fifa_ng_db-meta.xml" });
            
            var tableProperty = dbFileType.GetProperty("Table"); // DbFile.Table is an array or collection
            var tableCollection = tableProperty.GetValue(dbFile, null);
            
            // Invoke Table[string] indexer
            var itemProp = tableCollection.GetType().GetProperty("Item", new Type[] { typeof(string) });
            object playersTable = itemProp.GetValue(tableCollection, new object[] { "players" });
            
            var recordsProp = playersTable.GetType().GetProperty("Records");
            var records = (System.Collections.IList)recordsProp.GetValue(playersTable, null);
            
            Console.WriteLine("Loaded " + records.Count + " players from fifa_ng_db.db!");

            Console.WriteLine("Instantiating Language...");
            object langFile = Activator.CreateInstance(langType, new object[] { @"M:\FUN\GAMES\FIFA 15\data\loc\eng_us.db", @"M:\FUN\GAMES\FIFA 15\data\loc\eng_us-meta.xml" });
            
            // Get string logic
            // Usually Language.GetString(int hashId) or similar. Let's inspect methods.
            var getStrMethod = langType.GetMethod("GetString", new Type[] { typeof(int) });
            if (getStrMethod != null)
            {
                Console.WriteLine("GetString method found!");
                
                // Get the first record
                object firstRecord = records[0];
                var getIntMethod = firstRecord.GetType().GetMethod("GetInt", new Type[] { typeof(string) });
                
                int fnId = (int)getIntMethod.Invoke(firstRecord, new object[] { "firstnameid" });
                int lnId = (int)getIntMethod.Invoke(firstRecord, new object[] { "lastnameid" });
                
                string fn = (string)getStrMethod.Invoke(langFile, new object[] { fnId });
                string ln = (string)getStrMethod.Invoke(langFile, new object[] { lnId });
                
                Console.WriteLine("First Player: " + fn + " " + ln);
            }
            else
            {
                // Inspect methods of Language
                foreach(var m in langType.GetMethods()) {
                    if (m.Name.Contains("String") || m.Name.Contains("Get"))
                        Console.WriteLine("Language Method: " + m.Name);
                }
            }

        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}
