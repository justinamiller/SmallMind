# SmallMind Scripts

This directory contains utility scripts for running demos and common tasks.

## Available Scripts

### ITIL Demo Scripts

- **`run-itil-demo.sh`** - Linux/macOS shell script to run the ITIL v4 RAG demo
- **`run-itil-demo.bat`** - Windows batch script to run the ITIL v4 RAG demo

These scripts demonstrate SmallMind's Retrieval-Augmented Generation (RAG) capabilities using the ITIL v4 framework dataset. For more information about the ITIL demo, see [/docs/features/ITIL_DEMO_GUIDE.md](../docs/features/ITIL_DEMO_GUIDE.md).

## Usage

### Linux/macOS

```bash
chmod +x run-itil-demo.sh
./run-itil-demo.sh
```

### Windows

```cmd
run-itil-demo.bat
```

## Adding New Scripts

When adding new scripts to this directory:
1. Use clear, descriptive names
2. Include appropriate shebang lines for shell scripts
3. Make shell scripts executable (`chmod +x`)
4. Document the script purpose and usage in this README
5. Add comments in the script itself explaining key steps
