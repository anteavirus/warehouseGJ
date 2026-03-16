#!/usr/bin/env python3
"""
Project Cleaner – Copy a project, filter swear words from comments,
and add apology comments for swear words found in code identifiers.
"""

import os
import re
import shutil
import sys
import argparse
from pathlib import Path

# ----------------------------------------------------------------------
# Default swear words (hardcoded)
DEFAULT_SWEAR_WORDS = {
    'badword', 'swearword', 'damn', 'hell', 'crap', 'shit', 'fuck',
    'ass', 'bitch', 'bastard', 'piss', 'dick', 'cock', 'pussy'
}

# Map file extensions to single‑line comment symbols (lowercase extension)
COMMENT_SYMBOLS = {
    '.py': '#',
    '.rb': '#',
    '.pl': '#',
    '.pm': '#',
    '.r': '#',
    '.sh': '#',
    '.bash': '#',
    '.zsh': '#',
    '.js': '//',
    '.jsx': '//',
    '.ts': '//',
    '.tsx': '//',
    '.c': '//',
    '.cpp': '//',
    '.h': '//',
    '.hpp': '//',
    '.java': '//',
    '.cs': '//',
    '.go': '//',
    '.php': '//',
    '.swift': '//',
    '.kt': '//',
    '.kts': '//',
    '.rs': '//',
    '.scala': '//',
    '.groovy': '//',
    '.lua': '--',
    '.sql': '--',
    '.hs': '--',
    '.elm': '--',
    '.ml': '(*',          # OCaml – single line not really, but we'll treat as comment start
    '.m': '%',            # MATLAB/Octave
    '.ps1': '#',
    '.vim': '"',
}
# For languages with block comments we only use the single‑line variant for simplicity.
# Extend the dictionary as needed.

# ----------------------------------------------------------------------
def load_swear_words(file_path=None):
    """Return a set of lowercase swear words from a file (one per line) or the default set."""
    if file_path:
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                words = {line.strip().lower() for line in f if line.strip()}
            if words:
                return words
        except Exception as e:
            print(f"Warning: Could not read swear words file '{file_path}': {e}", file=sys.stderr)
    return DEFAULT_SWEAR_WORDS

# ----------------------------------------------------------------------
def contains_swear_in_identifiers(text, swear_words):
    """
    Return True if any swear word appears as a component of an identifier in `text`.
    Identifiers are split by underscores and camelCase/PascalCase boundaries.
    """
    # Find all identifier-like tokens (letters, digits, underscores)
    tokens = re.findall(r'[a-zA-Z_][a-zA-Z0-9_]*', text)
    for token in tokens:
        # Split by underscore first
        for part in token.split('_'):
            # Split camelCase/PascalCase into components
            # Pattern: sequences of lowercase possibly preceded by one uppercase,
            # or sequences of uppercase (acronyms) followed by another uppercase or end.
            components = re.findall(r'[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)', part)
            if not components:
                # No case boundaries (e.g., all lowercase or all uppercase) – keep whole part
                components = [part]
            for comp in components:
                if comp.lower() in swear_words:
                    return True
    return False

# ----------------------------------------------------------------------
def should_skip_file(file_path, script_path, swear_words_file):
    """Return True if the file is the script itself or the swear words file (if inside source)."""
    abs_file = os.path.abspath(file_path)
    if abs_file == os.path.abspath(script_path):
        return True
    if swear_words_file and os.path.abspath(swear_words_file) == abs_file:
        return True
    return False

# ----------------------------------------------------------------------
def process_file(input_path, output_path, comment_symbol, swear_words):
    """Read a file, filter swear words in comments, add apology for code, and write to output_path."""
    try:
        with open(input_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except UnicodeDecodeError:
        # Fallback for binary files – just copy
        shutil.copy2(input_path, output_path)
        return
    except Exception as e:
        print(f"Error reading {input_path}: {e}", file=sys.stderr)
        shutil.copy2(input_path, output_path)
        return

    new_lines = []
    # Pre‑compile word patterns for comment replacement (whole word, case‑insensitive)
    patterns = [re.compile(r'\b' + re.escape(w) + r'\b', re.IGNORECASE) for w in swear_words]

    for line in lines:
        original_line = line.rstrip('\n')
        # Special case: shebang line – treat as code only
        if line.lstrip().startswith('#!'):
            code_part = original_line
            comment_part = ''
        else:
            # Split at the first occurrence of the comment symbol
            idx = original_line.find(comment_symbol)
            if idx != -1:
                code_part = original_line[:idx]
                comment_part = original_line[idx:]
            else:
                code_part = original_line
                comment_part = ''

        # Check for swear words in code identifiers (improved detection)
        swear_in_code = contains_swear_in_identifiers(code_part, swear_words)

        # Replace swear words in comment part
        if comment_part:
            for p in patterns:
                comment_part = p.sub('[_____]', comment_part)

        # Reconstruct line
        modified_line = code_part + comment_part

        # If swear words were found in code, append an apology comment
        if swear_in_code:
            if modified_line and not modified_line.endswith(' '):
                modified_line += ' '
            modified_line += comment_symbol + ' Apology for bad naming conventions.'

        new_lines.append(modified_line + '\n')

    # Write the processed lines
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

# ----------------------------------------------------------------------
def copy_project(src_dir, dst_dir, script_path, swear_words, swear_words_file):
    """Recursively copy src_dir to dst_dir, processing files with known comment symbols."""
    for root, dirs, files in os.walk(src_dir):
        # Compute corresponding path in destination
        rel_path = os.path.relpath(root, src_dir)
        dest_root = os.path.join(dst_dir, rel_path) if rel_path != '.' else dst_dir

        for file in files:
            src_file = os.path.join(root, file)
            dst_file = os.path.join(dest_root, file)

            # Skip the script itself and the swear words file (if inside source)
            if should_skip_file(src_file, script_path, swear_words_file):
                print(f"Skipping excluded file: {src_file}")
                continue

            # Determine file extension and comment symbol
            ext = os.path.splitext(file)[1].lower()
            comment_symbol = COMMENT_SYMBOLS.get(ext)

            if comment_symbol:
                # Process the file (text)
                print(f"Processing: {src_file}")
                process_file(src_file, dst_file, comment_symbol, swear_words)
            else:
                # Just copy binary or unknown file types
                os.makedirs(dest_root, exist_ok=True)
                shutil.copy2(src_file, dst_file)

# ----------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(
        description="Copy a project, filter swear words from comments, and add apologies for bad naming."
    )
    parser.add_argument('source', nargs='?', default='.',
                        help='Source directory to copy (default: current directory)')
    parser.add_argument('-o', '--output', help='Output directory (default: source + "_filtered")')
    parser.add_argument('-w', '--swearwords', help='Path to a text file with swear words (one per line)')
    args = parser.parse_args()

    src_dir = os.path.abspath(args.source)
    if not os.path.isdir(src_dir):
        print(f"Error: Source directory '{src_dir}' does not exist.", file=sys.stderr)
        sys.exit(1)

    # Determine output directory
    if args.output:
        dst_dir = os.path.abspath(args.output)
    else:
        dst_dir = src_dir + '_filtered'

    # Load swear words
    swear_words_file = os.path.abspath(args.swearwords) if args.swearwords else None
    swear_words = load_swear_words(swear_words_file)

    # Path of this script (to exclude from copying)
    script_path = os.path.abspath(__file__)

    print(f"Copying '{src_dir}' to '{dst_dir}'")
    print(f"Using swear words: {sorted(swear_words)}")
    if swear_words_file:
        print(f"Swear words file: {swear_words_file} (will be excluded from copy if inside source)")

    try:
        copy_project(src_dir, dst_dir, script_path, swear_words, swear_words_file)
        print("Done.")
    except Exception as e:
        print(f"Error during copying: {e}", file=sys.stderr)
        sys.exit(1)

# ----------------------------------------------------------------------
if __name__ == '__main__':
    main()