#!/usr/bin/env python3
"""
Normalize AssignedRoute values in LatestRiders_parsed.csv to RouteTypes names.

Reads `LatestRiders_parsed.csv` and writes
`LatestRiders_parsed_normalized.csv` with AssignedRoute mapped to the
canonical names used in the RouteTypes table:
 - Kent Tiger Ride
 - Cruiser Classic
 - Darling Buds

Run:
    python scripts/normalize_assigned_routes.py
"""
import csv
import os
import sys

HERE = os.path.dirname(__file__)
INFILE = os.path.normpath(os.path.join(HERE, '..', 'LatestRiders_parsed.csv'))
OUTFILE = os.path.normpath(os.path.join(HERE, '..', 'LatestRiders_parsed_normalized.csv'))

def normalize_route(route):
    if not route:
        return ''
    r = route.lower()
    # Map known patterns to canonical RouteTypes names
    if 'kent' in r and 'tiger' in r:
        return 'Kent Tiger Ride'
    if 'cruiser' in r and 'classic' in r:
        return 'Cruiser Classic'
    if 'darling' in r:
        return 'Darling Buds'
    # Fallback: strip price and parentheses, then title-case what's left
    s = route
    if '£' in s:
        s = s.split('£', 1)[0]
    if '(' in s:
        s = s.split('(', 1)[0]
    s = s.strip()
    if not s:
        return ''
    return ' '.join(word.capitalize() for word in s.split())

def main():
    if not os.path.exists(INFILE):
        print(f'Input file not found: {INFILE}')
        sys.exit(1)

    with open(INFILE, 'r', encoding='utf-8', newline='') as fin:
        reader = csv.DictReader(fin)
        fieldnames = reader.fieldnames or []
        rows = list(reader)

    if 'AssignedRoute' not in fieldnames:
        print('Expected column "AssignedRoute" not found in input CSV headers:', fieldnames)
        sys.exit(1)

    with open(OUTFILE, 'w', encoding='utf-8', newline='') as fout:
        writer = csv.DictWriter(fout, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            route = (row.get('AssignedRoute') or '').strip()
            row['AssignedRoute'] = normalize_route(route)
            writer.writerow(row)

    print('Wrote', OUTFILE)

if __name__ == '__main__':
    main()
