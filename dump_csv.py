import pandas as pd

try:
    file_path = r"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\Overlays Offset Mapping Ver 2.0.xlsx"
    xl = pd.ExcelFile(file_path)
    if '9002 CGFE' in xl.sheet_names:
        df = xl.parse('9002 CGFE')
        df.to_csv(r"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\9002_mapping.csv", index=False)
        print("Exported 9002_mapping.csv")
    if '9001 and 9009' in xl.sheet_names:
        df = xl.parse('9001 and 9009')
        df.to_csv(r"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\9001_mapping.csv", index=False)
        print("Exported 9001_mapping.csv")
except Exception as e:
    print(f"Error: {e}")
