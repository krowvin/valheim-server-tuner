// ValheimNetPatcher - Patch Valheim's assembly to increase the network send queue limit
// USE AT YOUR OWN RISK - Improper use may cause instability or data loss
// MAKE BACKUPS of your game data and assemblies before using
// Reference: https://jamesachambers.com/revisiting-fixing-valheim-lag-modifying-send-receive-limits/

using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class P
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: ValheimNetPatcher <assembly-path | managed-dir> [newLimitInt]");
            return 2;
        }

        var newLimit = Environment.GetEnvironmentVariable("VALHEIM_SEND_QUEUE_LIMIT");
        if (string.IsNullOrWhiteSpace(newLimit) && args.Length > 1) newLimit = args[1];
        if (string.IsNullOrWhiteSpace(newLimit)) newLimit = "30720";
        if (!int.TryParse(newLimit, out var target))
        {
            Console.Error.WriteLine($"Invalid limit: {newLimit}");
            return 3;
        }

        // temp dir
        var tmpRoot = Environment.GetEnvironmentVariable("TMPDIR");
        if (string.IsNullOrWhiteSpace(tmpRoot)) tmpRoot = "/tmp";
        Directory.CreateDirectory(tmpRoot);

        var input = args[0];
        if (Directory.Exists(input))
        {
            // If a directory is provided, these are the most likely candidates across builds.
            var candidates = new[]
            {
                Path.Combine(input, "assembly_valheim.dll"),
                Path.Combine(input, "Assembly-CSharp.dll")
            }.Where(File.Exists).ToArray();

            if (candidates.Length == 0)
            {
                Console.Error.WriteLine($"No candidate DLLs found in: {input}");
                return 4;
            }

            int totalEdits = 0, patchedFiles = 0, exitCode = 0;
            foreach (var asm in candidates)
            {
                var rc = PatchOne(asm, target, tmpRoot, out var edits);
                if (rc == 0 && edits > 0) { patchedFiles++; totalEdits += edits; }
                // keep last nonzero rc as exit code if none succeeded
                if (rc != 0) exitCode = rc;
            }

            if (patchedFiles == 0 && exitCode != 0) return exitCode;
            return 0;
        }
        else
        {
            // Single file mode
            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Assembly not found: {input}");
                return 4;
            }
            return PatchOne(input, target, tmpRoot, out _);
        }
    }

    private static int PatchOne(string asmPath, int target, string tmpRoot, out int editsMade)
    {
        editsMade = 0;
        var asmFull = Path.GetFullPath(asmPath);
        var asmDir = Path.GetDirectoryName(asmFull)!;

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(asmDir);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadSymbols = false,
            InMemory = true
        };

        Console.WriteLine($"[patcher] Module file: {asmFull}");

        int hits = 0, edits = 0;
        // Read, modify in-memory
        using (var module = ModuleDefinition.ReadModule(asmFull, readerParams))
        {
            var asmName = module.Assembly?.Name?.Name ?? "(unknown)";
            Console.WriteLine($"[patcher] Assembly: {asmName}");

            bool hasZdoMan = module.Types.Any(t => t.Name == "ZDOMan");
            if (!hasZdoMan)
            {
                Console.WriteLine("[patcher] ZDOMan not found in this module; skipping.");
                return 0; // not the right file, don't error out
            }

            foreach (var type in module.Types)
            {
                if (type.Name != "ZDOMan") continue;
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    var il = method.Body.Instructions;
                    for (int i = 0; i < il.Count; i++)
                    {
                        var ins = il[i];
                        int? val = ins.OpCode.Code switch
                        {
                            Code.Ldc_I4 => (int)ins.Operand,
                            Code.Ldc_I4_S => (sbyte)ins.Operand,
                            _ => null
                        };
                        if (val is 10240)
                        {
                            hits++;
                            Console.WriteLine($"[patcher] Found 10240 in {type.FullName}::{method.Name} at IL index {i}");
                            if (target >= sbyte.MinValue && target <= sbyte.MaxValue)
                            {
                                ins.OpCode = OpCodes.Ldc_I4_S;
                                ins.Operand = unchecked((sbyte)target);
                            }
                            else
                            {
                                ins.OpCode = OpCodes.Ldc_I4;
                                ins.Operand = target;
                            }
                            edits++;
                            Console.WriteLine($"[patcher]  -> Replaced with {target}");
                        }
                    }
                }
            }

            if (edits == 0)
            {
                Console.WriteLine("[patcher] No 10240 constants found in ZDOMan. Possibly already patched or code changed.");
                return 5;
            }

            // Write to a temp file in /tmp, then replace target
            var tmpFile = Path.Combine(tmpRoot, $"{Path.GetFileName(asmFull)}.{Guid.NewGuid():N}.patched");
            var writerParams = new WriterParameters { WriteSymbols = false };
            module.Write(tmpFile, writerParams);

            // Replace original
            try
            {
                File.Replace(tmpFile, asmFull, asmFull + ".bak", ignoreMetadataErrors: true);
            }
            catch
            {
                if (File.Exists(asmFull)) File.Delete(asmFull);
                File.Move(tmpFile, asmFull);
            }

            Console.WriteLine($"[patcher] Patched {edits} occurrence(s) (found {hits}) to {target} in {asmFull}");
        }

        // Audit final on disk file
        using (var mod2 = ModuleDefinition.ReadModule(asmFull, new ReaderParameters { AssemblyResolver = resolver }))
        {
            int still10240 = 0, nowTarget = 0;
            foreach (var t in mod2.Types)
            {
                if (t.Name != "ZDOMan") continue;
                foreach (var m in t.Methods)
                {
                    if (!m.HasBody) continue;
                    foreach (var ins in m.Body.Instructions)
                    {
                        int? v = ins.OpCode.Code switch
                        {
                            Code.Ldc_I4 => (int)ins.Operand,
                            Code.Ldc_I4_S => (sbyte)ins.Operand,
                            _ => null
                        };
                        if (v is 10240) still10240++;
                        if (v == target) nowTarget++;
                    }
                }
            }
            Console.WriteLine($"[patcher] Audit: remaining 10240 = {still10240}, occurrences of {target} = {nowTarget}");
            editsMade = nowTarget;
            // success if audit shows at least one target and zero remaining 10240
            return still10240 == 0 && nowTarget > 0 ? 0 : 6;
        }
    }
}
