#!/usr/bin/env python3
"""
Generate a T-SQL script to delete existing riders and import riders from
LatestRiders_parsed_normalized.csv. RiderId will be set to `rider-<bib>`
where `<bib>` is the bib zero-padded to 3 digits (e.g. 001). BibNumber will
also be stored zero-padded to 3 digits.

The generated SQL uses a lookup to map the route name to `RouteTypeId`
via `SELECT RouteTypeId FROM RouteTypes WHERE Name = N'...'`.

Output: scripts/import_riders.sql
"""
import csv
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
IN_CSV = (HERE / '..' / 'LatestRiders_parsed_normalized.csv').resolve()
OUT_SQL = (HERE / 'import_riders.sql').resolve()

def quote_sql(s: str) -> str:
    # Return a SQL literal (N'...') or NULL
    if s is None:
        return 'NULL'
    s2 = s.strip()
    if s2 == '':
        return "N''"
    # escape single quotes
    s2 = s2.replace("'", "''")
    return f"N'{s2}'"

def bib_to_padded(bib_raw: str) -> str:
    if bib_raw is None:
        return ''
    b = bib_raw.strip()
    if b == '':
        return ''
    try:
        n = int(float(b))
        return f"{n:03d}"
    except Exception:
        # fallback: keep alnum chars
        s = ''.join(ch for ch in b if ch.isalnum())
        return s or b

def main():
    if not IN_CSV.exists():
        print('Input CSV not found:', IN_CSV)
        sys.exit(1)

    rows = []
    with IN_CSV.open('r', encoding='utf-8', newline='') as fh:
        reader = csv.DictReader(fh)
        for r in reader:
            rows.append(r)

    with OUT_SQL.open('w', encoding='utf-8', newline='') as out:
        out.write('-- Generated import script: deletes existing riders and imports new set\n')
        out.write('SET XACT_ABORT ON;\n')
        out.write('BEGIN TRANSACTION;\n')
        out.write("-- delete timing sessions then riders (FKs may cascade, but keep explicit)\n")
        out.write('DELETE FROM [TimingSessions];\n')
        out.write('DELETE FROM [Riders];\n')
        out.write('\n')

        out.write("-- Insert riders (RouteTypeId resolved by route name)\n")
        for r in rows:
            bib_raw = r.get('BibNumber', '')
            bib = bib_to_padded(bib_raw)
            if bib == '':
                # skip rows without bib
                continue
            rider_id = f"rider-{bib}"
            full_name = (r.get('FullName') or '').strip()
            email = (r.get('Email') or '').strip()
            phone = (r.get('MobileNumber') or '').strip()
            route = (r.get('AssignedRoute') or '').strip()
            category = ''

            rider_id_sql = quote_sql(rider_id)
            bib_sql = quote_sql(bib)
            full_name_sql = quote_sql(full_name)
            category_sql = quote_sql(category)
            route_sql = quote_sql(route)
            email_sql = 'NULL' if email == '' else quote_sql(email)
            phone_sql = 'NULL' if phone == '' else quote_sql(phone)

            # Use a subquery to resolve RouteTypeId from RouteTypes by Name
            if route == '':
                route_lookup = 'NULL'
            else:
                route_lookup = f"(SELECT TOP 1 RouteTypeId FROM RouteTypes WHERE Name = {route_sql})"

            out.write('-- {0} - {1}\n'.format(bib, full_name.replace("\n", ' ')))
            out.write('INSERT INTO [Riders] (RiderId, BibNumber, FullName, Category, RouteTypeId, Email, Phone, UpdatedAt)\n')
            out.write('VALUES ({rid}, {bib}, {name}, {cat}, {route}, {email}, {phone}, SYSDATETIMEOFFSET());\n'.format(
                rid=rider_id_sql,
                bib=bib_sql,
                name=full_name_sql,
                cat=category_sql,
                route=route_lookup,
                email=email_sql,
                phone=phone_sql
            ))
            out.write('\n')

        out.write('COMMIT TRANSACTION;\n')
        out.write("PRINT 'Rider import complete.';\n")

    print('Wrote', OUT_SQL)

if __name__ == '__main__':
    main()
