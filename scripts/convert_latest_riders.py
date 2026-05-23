#!/usr/bin/env python3
"""
Simple converter: LatestRiders.txt (TSV) -> LatestRiders_parsed.csv

Extracts: bib (Column 14), email, full name, mobile, assigned route.
"""
import csv
import os
import sys

HERE = os.path.dirname(__file__)
INFILE = os.path.normpath(os.path.join(HERE, '..', 'LatestRiders.txt'))
OUTFILE = os.path.normpath(os.path.join(HERE, '..', 'LatestRiders_parsed.csv'))

def find_index(headers, keywords, default):
    for i, h in enumerate(headers):
        lh = (h or '').lower()
        for kw in keywords:
            if kw in lh:
                return i
    return default

def main():
    if not os.path.exists(INFILE):
        print(f'Input file not found: {INFILE}')
        sys.exit(1)

    with open(INFILE, 'r', encoding='utf-8', newline='') as fin:
        reader = csv.reader(fin, delimiter='\t', quotechar='"')
        headers = next(reader, None)
        if headers is None:
            print('No header row found in input')
            sys.exit(1)

        bib_idx = find_index(headers, ['column 14', 'column14', 'column_14'], 1)
        email_idx = find_index(headers, ['email'], 2)
        name_idx = find_index(headers, ['full name', 'fullname', 'full name:'], 3)
        mobile_idx = find_index(headers, ['mobile'], 4)
        route_idx = find_index(headers, ['preferred route', 'preferredroute'], 9)

        with open(OUTFILE, 'w', encoding='utf-8', newline='') as fout:
            writer = csv.writer(fout)
            writer.writerow(['BibNumber','Email','FullName','MobileNumber','AssignedRoute'])
            for row in reader:
                # skip empty rows
                if not any((cell or '').strip() for cell in row):
                    continue
                bib = row[bib_idx].strip() if len(row) > bib_idx else ''
                email = row[email_idx].strip() if len(row) > email_idx else ''
                name = row[name_idx].strip() if len(row) > name_idx else ''
                mobile = row[mobile_idx].strip() if len(row) > mobile_idx else ''
                route = row[route_idx].strip() if len(row) > route_idx else ''
                writer.writerow([bib, email, name, mobile, route])

    print('Wrote', OUTFILE)

if __name__ == '__main__':
    main()
