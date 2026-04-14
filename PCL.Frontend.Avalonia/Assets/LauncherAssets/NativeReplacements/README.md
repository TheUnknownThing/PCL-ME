# Native Replacements SHA1 Notice

When modifying `native-replacements.json`, make sure every `artifact.sha1` is correct before commit.

You can either:
- verify and edit SHA1 values manually, or
- run:

```bash
python3 PCL.Frontend.Avalonia/scripts/recalculate_native_replacements_sha1.py
```

Optional check without writing changes:

```bash
python3 PCL.Frontend.Avalonia/scripts/recalculate_native_replacements_sha1.py --dry-run
```
