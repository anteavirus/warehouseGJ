#!/usr/bin/env python3
"""
File Compiler - Recursively compiles text files into a single document.
"""

import os
import sys
import argparse
from pathlib import Path
from datetime import datetime

def is_text_file(file_path):
    """Check if a file is likely text-based by reading first few bytes."""
    try:
        with open(file_path, 'rb') as f:
            chunk = f.read(1024)
            # Check for null bytes which indicate binary files
            if b'\x00' in chunk:
                return False
            
            # Try to decode as UTF-8 to verify it's text
            chunk.decode('utf-8', errors='ignore')
            return True
    except:
        return False

def should_include_file(file_path, include_extensions, exclude_patterns):
    """Determine if a file should be included based on extensions and patterns."""
    filename = file_path.name.lower()
    
    # Exclude based on patterns
    for pattern in exclude_patterns:
        if pattern in filename:
            return False
    
    # If no extensions specified, include all text files
    if not include_extensions:
        return is_text_file(file_path)
    
    # Check against included extensions
    ext = file_path.suffix.lower()
    if ext in include_extensions:
        return True
    
    # Also include files without extensions that match common names
    no_ext_names = ['readme', 'license', 'changelog', 'todo', 'notes']
    if any(name in filename for name in no_ext_names) and not ext:
        return True
    
    return False

def compile_files(base_path, output_file, include_extensions=None, exclude_patterns=None):
    """
    Recursively compile text files into a single output file.
    
    Args:
        base_path: Starting directory path
        output_file: Output file path
        include_extensions: List of file extensions to include (None = all text files)
        exclude_patterns: List of patterns to exclude
    """
    if exclude_patterns is None:
        exclude_patterns = []
    
    # Add script name to exclude patterns
    script_name = Path(__file__).name
    exclude_patterns.append(script_name)
    
    # Convert extensions to lowercase
    if include_extensions:
        include_extensions = [ext.lower() for ext in include_extensions]
    
    base_path = Path(base_path).resolve()
    output_path = Path(output_file).resolve()
    
    # Collect all files
    files_to_compile = []
    
    for root, dirs, files in os.walk(base_path):
        # Skip hidden directories (starting with .)
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        
        for file in files:
            file_path = Path(root) / file
            
            # Skip the output file
            if file_path.resolve() == output_path:
                continue
            
            if should_include_file(file_path, include_extensions, exclude_patterns):
                try:
                    # Get relative path for display
                    rel_path = file_path.relative_to(base_path)
                    files_to_compile.append((file_path, rel_path))
                except ValueError:
                    files_to_compile.append((file_path, file_path))
    
    # Sort files by path for organized output
    files_to_compile.sort(key=lambda x: str(x[1]))
    
    # Write to output file
    with open(output_path, 'w', encoding='utf-8') as out_f:
        # Write header
        out_f.write(f"Compiled Files Report\n")
        out_f.write(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        out_f.write(f"Base Directory: {base_path}\n")
        out_f.write(f"Total Files: {len(files_to_compile)}\n")
        out_f.write("=" * 80 + "\n\n")
        
        # Write each file's contents
        for i, (abs_path, rel_path) in enumerate(files_to_compile, 1):
            try:
                with open(abs_path, 'r', encoding='utf-8') as in_f:
                    content = in_f.read()
            except UnicodeDecodeError:
                # Try with different encoding
                try:
                    with open(abs_path, 'r', encoding='latin-1') as in_f:
                        content = in_f.read()
                except:
                    content = "[ERROR: Could not read file as text]"
            except Exception as e:
                content = f"[ERROR: {str(e)}]"
            
            # Write file header
            out_f.write(f"\n{'=' * 80}\n")
            out_f.write(f"File {i}: {rel_path}\n")
            out_f.write(f"Path: {abs_path}\n")
            out_f.write(f"{'=' * 80}\n\n")
            
            # Write content
            out_f.write(content)
            
            # Add spacing between files
            out_f.write("\n\n")
    
    return len(files_to_compile)

def main():
    parser = argparse.ArgumentParser(
        description='Compile text files from directory tree into a single file.'
    )
    parser.add_argument(
        'base_dir',
        nargs='?',
        default='.',
        help='Base directory to start from (default: current directory)'
    )
    parser.add_argument(
        '-o', '--output',
        default='compiled_output.txt',
        help='Output file name (default: compiled_output.txt)'
    )
    parser.add_argument(
        '-e', '--extensions',
        nargs='+',
        help='File extensions to include (e.g., .py .md .txt)'
    )
    parser.add_argument(
        '-x', '--exclude',
        nargs='+',
        default=[],
        help='Patterns to exclude (e.g., test_ *.tmp)'
    )
    parser.add_argument(
        '-p', '--preview',
        action='store_true',
        help='Preview files to be compiled without creating output'
    )
    
    args = parser.parse_args()
    
    # Check if base directory exists
    if not os.path.exists(args.base_dir):
        print(f"Error: Directory '{args.base_dir}' does not exist.")
        sys.exit(1)
    
    if args.preview:
        # Preview mode - just list files
        base_path = Path(args.base_dir).resolve()
        print(f"Files that would be compiled from: {base_path}")
        print("-" * 60)
        
        count = 0
        for root, dirs, files in os.walk(base_path):
            dirs[:] = [d for d in dirs if not d.startswith('.')]
            for file in files:
                file_path = Path(root) / file
                if should_include_file(file_path, args.extensions, args.exclude + [Path(__file__).name]):
                    rel_path = file_path.relative_to(base_path)
                    print(f"  {rel_path}")
                    count += 1
        
        print(f"\nTotal files: {count}")
        return
    
    # Compile files
    print(f"Compiling files from: {args.base_dir}")
    print(f"Output file: {args.output}")
    
    if args.extensions:
        print(f"Including extensions: {', '.join(args.extensions)}")
    if args.exclude:
        print(f"Excluding patterns: {', '.join(args.exclude)}")
    
    try:
        count = compile_files(
            args.base_dir,
            args.output,
            args.extensions,
            args.exclude
        )
        
        print(f"\nSuccessfully compiled {count} files into '{args.output}'")
        
        # Show file size
        if os.path.exists(args.output):
            size = os.path.getsize(args.output)
            if size > 1024 * 1024:
                print(f"Output file size: {size/(1024*1024):.2f} MB")
            elif size > 1024:
                print(f"Output file size: {size/1024:.2f} KB")
            else:
                print(f"Output file size: {size} bytes")
                
    except KeyboardInterrupt:
        print("\nOperation cancelled by user.")
        sys.exit(1)
    except Exception as e:
        print(f"Error: {str(e)}")
        sys.exit(1)

if __name__ == "__main__":
    main()