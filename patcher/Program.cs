// ValheimNetPatcher - Patch Valheim's assembly to increase the network send queue limit
// USE AT YOUR OWN RISK - Improper use may cause instability or data loss
// MAKE BACKUPS of your game data and assemblies before using
// Shout out to: 
// https://jamesachambers.com/revisiting-fixing-valheim-lag-modifying-send-receive-limits/

using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class P
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: ValheimNetPatcher <assembly-path> [newLimitInt]");
            return 2;
        }

        var asmPath = args[0];
        var newLimit = Environment.GetEnvironmentVariable("VALHEIM_SEND_QUEUE_LIMIT");
        if (string.IsNullOrWhiteSpace(newLimit) && args.Length > 1) newLimit = args[1];
        if (string.IsNullOrWhiteSpace(newLimit)) newLimit = "30720";
        if (!int.TryParse(newLimit, out var target))
        {
            Console.Error.WriteLine($"Invalid limit: {newLimit}");
            return 3;
        }
        if (!File.Exists(asmPath))
        {
            Console.Error.WriteLine($"Assembly not found: {asmPath}");
            return 4;
        }

        var asmDir = Path.GetDirectoryName(Path.GetFullPath(asmPath))!;
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(asmDir);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = true,
            InMemory = true,
            ReadSymbols = false
        };

        using var module = ModuleDefinition.ReadModule(asmPath, readerParams);

        int hits = 0, edits = 0;
        foreach (var type in module.Types)
        {
            if (type.Name != "ZDOMan") continue; // stay surgical
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                var body = method.Body;
                var il = body.Instructions;
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
            // Throw an error to fail the CI step
            return 5;
        }
        else
        {
            module.Write(new WriterParameters { WriteSymbols = false });
            Console.WriteLine($"[patcher] Patched {edits} occurrence(s) (found {hits}) to {target} in {asmPath}");
        }

        // Audit pass: count any remaining 10240 and any target constants in ZDOMan
        using var mod2 = ModuleDefinition.ReadModule(asmPath, new ReaderParameters { AssemblyResolver = resolver });
        int still10240 = 0, nowTarget = 0;
        foreach (var type in mod2.Types)
        {
            if (type.Name != "ZDOMan") continue;
            foreach (var m in type.Methods)
            {
                if (!m.HasBody) continue;
                foreach (var ins in m.Body.Instructions)
                {
                    int? val = ins.OpCode.Code switch
                    {
                        Code.Ldc_I4 => (int)ins.Operand,
                        Code.Ldc_I4_S => (sbyte)ins.Operand,
                        _ => null
                    };
                    if (val is 10240) still10240++;
                    if (val == target) nowTarget++;
                }
            }
        }
        Console.WriteLine($"[patcher] Audit: remaining 10240 = {still10240}, occurrences of {target} = {nowTarget}");
        return 0;
    }
}
