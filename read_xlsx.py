import pandas as pd
import sys

try:
    file_path = r"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\Overlays Offset Mapping Ver 2.0.xlsx"
    xl = pd.ExcelFile(file_path)
    for sheet_name in xl.sheet_names:
        print(f"\n--- Sheet: {sheet_name} ---")
        df = xl.parse(sheet_name)
        print(df.head(20).to_string())
except Exception as e:
    print(f"Error: {e}")
