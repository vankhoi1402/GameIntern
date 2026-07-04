#!/usr/bin/env python3
"""Rebuild Level_XXX assets and LevelCatalog from level.csv folders (no Unity required)."""

import os
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEVEL_DATA = ROOT / "Assets" / "_Gamenay" / "Level" / "LevelData"
LEVEL_SCRIPT_GUID = "f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6"
CATALOG_SCRIPT_GUID = "f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7"
GRID_COLUMNS = 5


def new_guid() -> str:
    return uuid.uuid4().hex


def read_guid(meta_path: Path) -> str | None:
    if not meta_path.exists():
        return None
    text = meta_path.read_text(encoding="utf-8")
    match = re.search(r"^guid:\s*(\w+)", text, re.MULTILINE)
    return match.group(1) if match else None


def write_text_meta(path: Path, guid: str) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_folder_meta(path: Path, guid: str) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def write_asset_meta(path: Path, guid: str) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def is_header(line: str) -> bool:
    lower = line.lower()
    return lower.startswith("row,") or lower.startswith("row\t") or "col0" in lower or "col1" in lower


def split_csv_line(line: str) -> list[str]:
    return line.split("\t") if "\t" in line else line.split(",")


def parse_level_csv(level_id: int, csv_text: str) -> tuple[int, list[dict]]:
    time_limit = 0
    rows: list[dict] = []

    lines = [line for line in re.split(r"\r?\n", csv_text) if line.strip()]
    start = 1 if lines and is_header(lines[0]) else 0

    for line in lines[start:]:
        line = line.strip()
        if not line or line.startswith("#"):
            continue

        cols = split_csv_line(line)
        if len(cols) < GRID_COLUMNS + 1:
            continue

        try:
            row_index = int(cols[0].strip())
        except ValueError:
            continue

        block_types = []
        for c in range(GRID_COLUMNS):
            value = cols[c + 1].strip()
            block_types.append(value if value else "")

        if row_index == 0 and len(cols) > GRID_COLUMNS + 1:
            try:
                time_limit = max(0, int(cols[GRID_COLUMNS + 1].strip()))
            except ValueError:
                pass

        rows.append({"Row": row_index, "ColBlockTypes": block_types})

    return time_limit, rows


def yaml_string_list(values: list[str], indent: str) -> list[str]:
    lines = [f"{indent}ColBlockTypes:"]
    for value in values:
        if value:
            lines.append(f"{indent}- {value}")
        else:
            lines.append(f"{indent}- ")
    return lines


def write_level_asset(path: Path, level_id: int, time_limit: int, rows: list[dict]) -> None:
    folder_name = f"Level_{level_id:03d}"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {LEVEL_SCRIPT_GUID}, type: 3}}",
        f"  m_Name: {folder_name}",
        "  m_EditorClassIdentifier: ",
        f"  LevelId: {level_id}",
        "  VisibleRows: 0",
        "  Rows:",
    ]

    for row in rows:
        lines.append(f"  - Row: {row['Row']}")
        lines.extend(yaml_string_list(row["ColBlockTypes"], "    "))

    lines.append(f"  TimeLimitSeconds: {time_limit}")
    lines.append("")

    path.write_text("\n".join(lines), encoding="utf-8")


def ensure_csv_meta(folder: Path, level_id: int) -> str:
    csv_meta = folder / "level.csv.meta"
    if csv_meta.exists():
        guid = read_guid(csv_meta)
        if guid:
            return guid

    root_meta = LEVEL_DATA / f"level_{level_id}.csv.meta"
    guid = read_guid(root_meta) or new_guid()
    write_text_meta(csv_meta, guid)
    return guid


def ensure_folder_meta(folder: Path) -> None:
    folder_meta = folder / f"{folder.name}.meta"
    if folder_meta.exists():
        return
    if (folder / ".meta").exists():
        return
    # Unity uses FolderName.meta for subfolders
    meta_path = Path(str(folder) + ".meta")
    if meta_path.exists():
        return
    write_folder_meta(meta_path, new_guid())


def ensure_asset_meta(asset_path: Path) -> str:
    meta_path = Path(str(asset_path) + ".meta")
    guid = read_guid(meta_path)
    if guid:
        return guid
    guid = new_guid()
    write_asset_meta(meta_path, guid)
    return guid


def write_catalog(entries: list[tuple[int, str, str]]) -> None:
    catalog_path = LEVEL_DATA / "LevelCatalog.asset"
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {CATALOG_SCRIPT_GUID}, type: 3}}",
        "  m_Name: LevelCatalog",
        "  m_EditorClassIdentifier: ",
        "  Levels:",
    ]

    for level_id, csv_guid, asset_guid in entries:
        lines.extend(
            [
                f"  - LevelId: {level_id}",
                f"    LevelCsv: {{fileID: 4900000, guid: {csv_guid}, type: 3}}",
                f"    LevelAsset: {{fileID: 11400000, guid: {asset_guid}, type: 2}}",
            ]
        )

    lines.append("")
    catalog_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    entries: list[tuple[int, str, str]] = []

    for level_id in range(1, 41):
        folder = LEVEL_DATA / f"Level_{level_id:03d}"
        csv_path = folder / "level.csv"
        asset_path = folder / f"Level_{level_id:03d}.asset"

        if not csv_path.exists():
            raise FileNotFoundError(f"Missing {csv_path}")

        ensure_folder_meta(folder)
        csv_guid = ensure_csv_meta(folder, level_id)

        csv_text = csv_path.read_text(encoding="utf-8-sig")
        time_limit, rows = parse_level_csv(level_id, csv_text)
        write_level_asset(asset_path, level_id, time_limit, rows)
        asset_guid = ensure_asset_meta(asset_path)

        entries.append((level_id, csv_guid, asset_guid))
        print(f"Level {level_id:02d}: rows={len(rows)}, time={time_limit}, csv={csv_guid}, asset={asset_guid}")

    write_catalog(entries)
    print(f"\nRebuilt LevelCatalog with {len(entries)} levels.")


if __name__ == "__main__":
    main()
