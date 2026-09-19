# Expanded nationality name pools — sources and methodology

This package preserves every pre-existing row and weight. New rows were appended at weight `1` so the implemented core distributions remain stable while rare/historical/variant forms become available.

All output remains UTF-8, NFC-normalized, Latin alphabet (with diacritics where appropriate). East-Slavic surnames remain in canonical masculine/family form, matching the existing runtime policy.

## German
- Gesellschaft für deutsche Sprache (GfdS), first-name database and annual name statistics: https://gfds.de/vornamendatenbank/ and https://gfds.de/vornamen/beliebteste-vornamen/
- GfdS overview of German family names and common surnames: https://gfds.de/vornamen/familiennamen/
- Wiktionary German male/female given-name and surname categories, used as a broad attestation cross-check.

## Jewish (Ashkenazi / Central-Eastern European)
- JewishGen Poland Collection and JewishGen Yizkor Book Master Name Index: https://www.jewishgen.org/databases/poland/ and https://www.jewishgen.org/databases/yizkor/Names/
- JewishGen notes on given names used by Jews in Poland: https://www.jewishgen.org/infofiles/poland/Q2.htm
- Wiktionary Jewish surname appendix and Yiddish/Hebrew surname categories; Behind the Name Yiddish/Ashkenazi given-name entries used as secondary attestation.

## Ukrainian
- Ukrainian State Migration Service transliteration rules/checker: https://dmsu.gov.ua/services/transliteration.html
- Ridni Ukrainian surname/name statistics: https://stats.ridni.org/ and https://ridni.org/
- Wiktionary Ukrainian male/female given-name and surname categories used for broad attestation.

## Belarusian
- Wiktionary Belarusian male/female given-name and surname categories.
- Forebears Belarus surname distribution used to verify broad modern occurrence: https://forebears.io/belarus/surnames
- Belarusian/East-Slavic naming references used to distinguish native and shared regional forms.

## Russian
- Wiktionary Russian male/female given-name and surname categories, including historical/ecclesiastical forms.
- Common modern surname distributions were used only as occurrence checks; rare additions remain weight 1.

## Lithuanian
- VLKK citizen given-name database, based on the Lithuanian Population Register: https://vardai.vlkk.lt/
- Lithuanian Language Institute surname database background: https://pavardes.lki.lt/?pg=english
- Wiktionary Lithuanian male/female given-name and male-surname categories used as an additional attestation check.

## Design note
The expansion deliberately stops before mechanically generating surname variants. A row was added only when the form is a recognizable attested name/surname or a standard transliteration/orthographic variant of one. This keeps the long tail large without turning it into synthetic morphology.
