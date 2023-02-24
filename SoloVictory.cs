using p5rpc.SoloVictory.Configuration;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Definitions;
using Reloaded.Memory.Sigscan.Definitions.Structs;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;

namespace p5rpc.SoloVictory
{
    internal unsafe class SoloVictory
    {
        internal delegate ulong FUN_14099bf50(long a1, long a2);

        internal IHook<FUN_14099bf50> _FUN_14099bf50;

        internal Memory memory;
        internal long SoloCheck { get; set; }
        internal byte[] JumpPatch { get; set; } = new byte[2];
        public SoloVictory(IReloadedHooks hooks, ILogger logger, IModLoader modLoader, Config config)
        {
            memory = Memory.Instance;

            modLoader.GetController<IStartupScanner>().TryGetTarget(out var scanner);

            using var thisProcess = Process.GetCurrentProcess();
            long baseAddress = thisProcess.MainModule.BaseAddress.ToInt64();

            JumpPatch[0] = 0x48;
            JumpPatch[1] = 0xe9;

            scanner.AddMainModuleScan("0F 84 ?? ?? ?? ?? 80 BD ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 4D ??", (result) =>
            {
                if (!result.Found)
                {
                    logger.WriteLine("Could not find Solo Joker Check");
                    return;
                }

                SoloCheck = baseAddress + result.Offset;
                logger.WriteLine($"Found Solo Joker Check at 0x{SoloCheck:X}");

                memory.SafeWriteRaw((nuint)SoloCheck, JumpPatch);
            });

            if (!config.ForceSolo)
            {
                scanner.AddMainModuleScan("48 8B C4 48 89 50 ?? 48 89 48 ?? 55 53 56 48 8D A8 ?? ?? ?? ??", (result) =>
                {
                    if (!result.Found)
                    {
                        logger.WriteLine("Could not find FUN_14099bf50");
                        return;
                    }

                    long address = baseAddress + result.Offset;
                    logger.WriteLine($"Found FUN_14099bf50 at 0x{address:X}");

                    _FUN_14099bf50 = hooks.CreateHook<FUN_14099bf50>((long a1, long a2) =>
                    {
                        var random = new Random();
                        
                        if ((random.Next() & 1) == 1) //Team
                        {
                            JumpPatch[0] = 0x0f;
                            JumpPatch[1] = 0x84;
                        }
                        else //Solo
                        {
                            JumpPatch[0] = 0x48;
                            JumpPatch[1] = 0xe9;
                        }


                        memory.SafeWriteRaw((nuint)SoloCheck, JumpPatch);

                        return _FUN_14099bf50.OriginalFunction(a1, a2);
                    }, address).Activate();
                });
            }
        }
    }
}
