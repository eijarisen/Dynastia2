# Validation report

Expanded pools: German, Jewish, Ukrainian, Belarusian, Russian, Lithuanian.

- german: male 210 (+110), female 197 (+97), surnames 852 (+352)
- jewish: male 160 (+60), female 151 (+51), surnames 874 (+374)
- ukrainian: male 178 (+78), female 138 (+38), surnames 623 (+123)
- belarusian: male 152 (+52), female 132 (+32), surnames 645 (+145)
- russian: male 244 (+144), female 162 (+62), surnames 582 (+82)
- lithuanian: male 204 (+104), female 152 (+52), surnames 666 (+166)

Total new rows: **2122**.

Checks performed:
- every pre-existing row and weight in the six edited CSVs remains in the same order as an exact parsed prefix;
- every added row has weight `1`;
- all nationality CSV names/surnames are unique within their file;
- all weights are positive integers;
- all alphabetic characters are Latin-script Unicode characters;
- names are NFC-normalized;
- `name_cultures.json` counts match the CSVs;
- every baseline file outside the six requested pools and the registry is byte-for-byte unchanged.

Validation: **PASS**
