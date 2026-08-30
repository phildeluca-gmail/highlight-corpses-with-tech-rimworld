# Highlight Corpses With Tech (RimWorld 1.5)

**Status: scaffold plus an agreed design. No behaviour is implemented
yet.** The mod loads and writes one line to the log. See
`HighlightCorpsesWithTech_Architecture.md` for the full design and the
handful of questions still open.

Highlights corpses that still carry recoverable bionics and implants, so
they can be triaged for harvesting rather than butchered or left to lie.

## Requirements

- RimWorld 1.5
- [Harmony](steam://url/CommunityFilePage/2009463077) - a standard
  dependency for RimWorld C# mods.

Nothing else. This mod is deliberately standalone: detection uses only
vanilla API, and it declares no dependency on any implant-recovery mod
(see architecture doc section 2).

## Building from source

This repo contains **no game DLLs**. They are copyrighted and not ours to
redistribute, so `lib/` is gitignored and empty on a fresh clone.

1. Run `setup-lib.bat` and give it your RimWorld install path. It copies
   the four required DLLs out of your own install and the Harmony
   Workshop mod into `lib/`. Nothing it copies is ever committed.
2. Build:
   ```
   cd HighlightCorpsesWithTech/Source/HighlightCorpsesWithTech
   dotnet build
   ```
   The DLL lands in `HighlightCorpsesWithTech/Assemblies/`.

   On a fresh clone the first build needs a restore pass:
   `msbuild /t:Restore,Rebuild`.

## Installing

Copy the `HighlightCorpsesWithTech/` folder - **not the repo root** -
into your RimWorld `Mods` directory, then enable it after Harmony.

## Committing

Run `git-commit-push.bat`. It stages everything, shows you what it is
about to commit, prompts for a message, commits and pushes.
