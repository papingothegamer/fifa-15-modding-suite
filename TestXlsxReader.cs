using System;
using System.Data;
using System.Data.OleDb;
using System.IO;

namespace XlsxReader
{
    class Program
    {
        static void Main(string[] args)
        {
            string filePath = @"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\Overlays Offset Mapping Ver 2.0.xlsx";
            string connectionString = string.Format("Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties=\"Excel 12.0 Xml;HDR=YES;IMEX=1;\"", filePath);
            
            try
            {
                using (OleDbConnection conn = new OleDbConnection(connectionString))
                {
                    conn.Open();
                    DataTable schemaTable = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });
                    
                    foreach (DataRow row in schemaTable.Rows)
                    {
                        string sheetName = row["TABLE_NAME"].ToString();
                        Console.WriteLine(string.Format("\n--- Sheet: {0} ---", sheetName));
                        
                        using (OleDbCommand cmd = new OleDbCommand(string.Format("SELECT * FROM [{0}]", sheetName), conn))
                        {
                            using (OleDbDataReader reader = cmd.ExecuteReader())
                            {
                                int colCount = reader.FieldCount;
                                int rows = 0;
                                while (reader.Read() && rows < 20)
                                {
                                    for (int i = 0; i < colCount; i++)
                                    {
                                        Console.Write(string.Format("{0}\t", reader[i]));
                                    }
                                    Console.WriteLine();
                                    rows++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
